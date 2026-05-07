using System.Text;
using System.Text.Json;
using System.Buffers;
using AssTrack.BridgeGateway.Adapters;
using AssTrack.Domain.Services;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace AssTrack.BridgeGateway.Services;

public sealed class MeshtasticMqttService(
    IOptions<BridgeGatewayOptions> options,
    DynamicBridgeFeedStore dynamicFeeds,
    ProviderAdapterRegistry registry,
    IAssTrackIngestClient ingestClient,
    BridgeFeedMonitor monitor,
    ILogger<MeshtasticMqttService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var running = new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var item in dynamicFeeds.Snapshot.Concat(options.Value.Feeds))
            {
                if (running.ContainsKey(item.Key)) continue;
                if (!item.Value.Enabled || !IsProvider(item.Value, "meshtastic")) continue;
                if (!IsTrue(Get(item.Value, "mqttEnabled", "subscriptionEnabled"))) continue;

                running[item.Key] = RunFeedAsync(item.Key, item.Value, monitor.GetResyncVersion(item.Key), stoppingToken);
            }

            foreach (var complete in running.Where(item => item.Value.IsCompleted).Select(item => item.Key).ToList())
            {
                running.Remove(complete);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task RunFeedAsync(string feedKey, BridgeFeedOptions feed, int resyncVersion, CancellationToken cancellationToken)
    {
        var host = Get(feed, "mqttHost", "host", "broker");
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("Meshtastic feed {FeedKey} is missing mqttHost.", feedKey);
            monitor.Log(feedKey, "warn", "Meshtastic feed is missing mqttHost.");
            return;
        }

        var topic = Get(feed, "mqttTopic", "topic") ?? "msh/US/2/json/LongFast/#";
        var port = Int(Get(feed, "mqttPort", "port")) ?? 1883;
        var username = Get(feed, "mqttUsername", "username");
        var password = Get(feed, "mqttPassword", "password");

        monitor.Update(feedKey, status =>
        {
            status.FeedId = feed.FeedId;
            status.Provider = feed.Provider;
            status.State = "connecting";
            status.Host = $"{host}:{port}";
            status.Topic = topic;
            status.LastError = null;
        });
        monitor.Log(feedKey, "info", $"Connecting to MQTT {host}:{port}, topic {topic}.");

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        var adapter = registry.Get("meshtastic");
        if (adapter is null)
        {
            logger.LogWarning("Meshtastic adapter is not registered.");
            return;
        }

        client.ApplicationMessageReceivedAsync += async args =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                var rawSummary = SummarizeMeshtasticPayload(args.ApplicationMessage.Topic, args.ApplicationMessage.Payload);
                var rawMetadata = ExtractMeshtasticMessageMetadata(args.ApplicationMessage.Topic, args.ApplicationMessage.Payload);
                monitor.RecordMessage(
                    feedKey,
                    "meshtastic",
                    args.ApplicationMessage.Topic,
                    rawMetadata.TrackerId,
                    rawMetadata.MessageType,
                    rawSummary,
                    PrettyJson(json));
                monitor.Update(feedKey, status =>
                {
                    status.MessagesReceived++;
                    status.LastMessageAt = DateTime.UtcNow;
                    status.State = "receiving";
                    status.LastError = null;
                });
                using var document = JsonDocument.Parse(json);
                var profile = ExtractMeshtasticDeviceProfile(document.RootElement);
                if (profile is not null)
                {
                    if (options.Value.DryRun)
                    {
                        monitor.Log(feedKey, "info", $"Dry run parsed profile {profile.ExternalTrackerId}; delivery skipped.");
                    }
                    else
                    {
                        var profileDelivery = await ingestClient.SendDeviceProfileAsync(feed.FeedId, profile, cancellationToken);
                        if (!profileDelivery.Success)
                        {
                            monitor.Log(feedKey, "warn", $"Profile delivery failed for {profile.ExternalTrackerId}: {profileDelivery.StatusCode}. {Summarize(profileDelivery.ResponseBody)}");
                        }
                        else
                        {
                            monitor.Log(feedKey, "debug", $"Profile delivered for {profile.ExternalTrackerId}: {FirstNonBlank(profile.LongName, profile.ShortName, profile.Label, "unnamed")}.");
                        }
                    }
                }

                var observations = await adapter.ParseAsync(new BridgeFeedContext(feedKey, feed, "meshtastic"), document.RootElement, cancellationToken);
                monitor.Update(feedKey, status => status.ObservationsParsed += observations.Count);
                foreach (var observation in observations)
                {
                    monitor.Update(feedKey, status => status.LastTrackerId = observation.ExternalTrackerId);
                    if (PositionSanityFilter.IsNullIslandNoise(observation.Latitude, observation.Longitude))
                    {
                        monitor.Log(feedKey, "debug", $"Ignored MQTT payload for {observation.ExternalTrackerId}: position is within 10 km of 0,0.");
                        continue;
                    }

                    if (options.Value.DryRun)
                    {
                        monitor.Log(feedKey, "info", $"Dry run parsed tracker {observation.ExternalTrackerId}; delivery skipped.");
                        continue;
                    }

                    var delivery = await ingestClient.SendAsync(feed.FeedId, observation, cancellationToken);
                    if (!delivery.Success)
                    {
                        monitor.Update(feedKey, status =>
                        {
                            status.DeliveryFailures++;
                            status.LastError = $"Delivery failed with {delivery.StatusCode}.";
                        });
                        var responseSummary = Summarize(delivery.ResponseBody);
                        monitor.Log(feedKey, "warn", $"Delivery failed for {observation.ExternalTrackerId}: {delivery.StatusCode}. {responseSummary}");
                        logger.LogWarning("Meshtastic delivery failed for {TrackerId}: {StatusCode}. {ResponseBody}", observation.ExternalTrackerId, delivery.StatusCode, responseSummary);
                    }
                    else
                    {
                        monitor.Update(feedKey, status =>
                        {
                            status.ObservationsDelivered++;
                            status.LastDeliveryAt = DateTime.UtcNow;
                            status.State = "delivering";
                            status.LastError = null;
                        });
                    }
                }
            }
            catch (BridgePayloadException ex)
            {
                monitor.Log(feedKey, "debug", $"Ignored MQTT payload: {ex.Message} {SummarizeMeshtasticPayload(args.ApplicationMessage.Topic, args.ApplicationMessage.Payload)}");
            }
            catch (Exception ex)
            {
                monitor.Update(feedKey, status => status.LastError = ex.Message);
                monitor.Log(feedKey, "warn", $"MQTT message handling failed: {ex.Message}");
                logger.LogWarning(ex, "Meshtastic MQTT message handling failed for feed {FeedKey}.", feedKey);
            }
        };

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(Get(feed, "mqttClientId", "clientId") ?? $"asstrack-{feedKey}-{Guid.NewGuid():N}");

        if (!string.IsNullOrWhiteSpace(username))
        {
            optionsBuilder.WithCredentials(username, password);
        }

        if (IsTrue(Get(feed, "mqttTls", "tls", "useTls")))
        {
            optionsBuilder.WithTlsOptions(new MqttClientTlsOptions { UseTls = true });
        }

        await client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        await client.SubscribeAsync(topic, cancellationToken: cancellationToken);
        monitor.Update(feedKey, status =>
        {
            status.State = "subscribed";
            status.ConnectedAt = DateTime.UtcNow;
            status.LastError = null;
        });
        monitor.Log(feedKey, "info", $"Subscribed to {topic}.");
        logger.LogInformation("Meshtastic feed {FeedKey} subscribed to {Topic} on {Host}:{Port}.", feedKey, topic, host, port);

        while (!cancellationToken.IsCancellationRequested && client.IsConnected && monitor.GetResyncVersion(feedKey) == resyncVersion)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }

        if (client.IsConnected)
        {
            await client.DisconnectAsync(cancellationToken: cancellationToken);
        }

        monitor.Update(feedKey, status => status.State = "disconnected");
        monitor.Log(feedKey, "info", "MQTT subscription disconnected.");
    }

    private static bool IsProvider(BridgeFeedOptions feed, string provider)
        => string.Equals(feed.Provider, provider, StringComparison.OrdinalIgnoreCase);

    private static string? Get(BridgeFeedOptions feed, params string[] keys)
        => keys.Select(key => feed.Settings.TryGetValue(key, out var value) ? value.Trim() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsTrue(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static int? Int(string? value)
        => int.TryParse(value, out var number) ? number : null;

    private static string Summarize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "No response body.";
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 300 ? normalized : $"{normalized[..300]}...";
    }

    private static string SummarizeMeshtasticPayload(string? topic, ReadOnlySequence<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var packet = Find(root, "packet") is { ValueKind: JsonValueKind.Object } packetElement ? packetElement : root;
            var decoded = Find(packet, "decoded") is { ValueKind: JsonValueKind.Object } decodedElement ? decodedElement : packet;
            var envelopePayload = Find(root, "payload") is { ValueKind: JsonValueKind.Object } payloadElement ? payloadElement : decoded;
            var position = Find(decoded, "position") is { ValueKind: JsonValueKind.Object } positionElement
                ? positionElement
                : Find(envelopePayload, "position") is { ValueKind: JsonValueKind.Object } payloadPositionElement
                    ? payloadPositionElement
                    : envelopePayload;

            var from = FirstNonBlank(String(packet, "fromId", "from"), String(root, "fromId", "from"), String(envelopePayload, "fromId", "from"));
            var to = FirstNonBlank(String(packet, "to"), String(root, "to"), String(envelopePayload, "to"));
            var sender = FirstNonBlank(String(root, "sender"), String(packet, "sender"));
            var type = FirstNonBlank(String(root, "type"), String(packet, "type"), String(decoded, "portnum", "portNum"));
            var latitude = Coordinate(position, "latitude", "lat", "latitudeI", "latitude_i");
            var longitude = Coordinate(position, "longitude", "lon", "lng", "longitudeI", "longitude_i");
            var hasLatitude = latitude is not null;
            var hasLongitude = longitude is not null;
            var positionState = PositionState(type, hasLatitude, hasLongitude, latitude, longitude);

            return $"topic={topic ?? "(none)"} type={DisplayType(type)} position={positionState} from={FormatNodeId(from)} to={FormatNodeId(to)} sender={FormatNodeId(sender)} hasLat={hasLatitude} hasLon={hasLongitude}.";
        }
        catch
        {
            return $"topic={topic ?? "(none)"} payloadSummaryUnavailable.";
        }
    }

    private static (string? TrackerId, string? MessageType) ExtractMeshtasticMessageMetadata(string? topic, ReadOnlySequence<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var packet = Find(root, "packet") is { ValueKind: JsonValueKind.Object } packetElement ? packetElement : root;
            var decoded = Find(packet, "decoded") is { ValueKind: JsonValueKind.Object } decodedElement ? decodedElement : packet;
            var envelopePayload = Find(root, "payload") is { ValueKind: JsonValueKind.Object } payloadElement ? payloadElement : decoded;

            var trackerId = FirstNonBlank(
                String(packet, "fromId", "from"),
                String(root, "fromId", "from"),
                String(envelopePayload, "fromId", "from"),
                String(root, "sender"));
            var messageType = FirstNonBlank(
                String(root, "type"),
                String(packet, "type"),
                String(decoded, "portnum", "portNum"),
                topic);

            return (FormatNodeId(trackerId), messageType);
        }
        catch
        {
            return (null, null);
        }
    }

    private static ProviderDeviceProfile? ExtractMeshtasticDeviceProfile(JsonElement root)
    {
        var packet = Find(root, "packet") is { ValueKind: JsonValueKind.Object } packetElement ? packetElement : root;
        var decoded = Find(packet, "decoded") is { ValueKind: JsonValueKind.Object } decodedElement ? decodedElement : packet;
        var decodedPayload = Find(decoded, "payload") is { ValueKind: JsonValueKind.Object } decodedPayloadElement ? decodedPayloadElement : default(JsonElement?);
        var envelopePayload = Find(root, "payload") is { ValueKind: JsonValueKind.Object } payloadElement
            ? payloadElement
            : decodedPayload ?? decoded;
        var nodeInfo = FirstObject(
            Find(envelopePayload, "nodeInfo", "node_info", "node"),
            Find(decoded, "nodeInfo", "node_info", "node"));
        var user = Find(envelopePayload, "user") is { ValueKind: JsonValueKind.Object } userElement ? userElement : envelopePayload;
        if (nodeInfo is { } nodeInfoElement && Find(nodeInfoElement, "user") is { ValueKind: JsonValueKind.Object } nodeInfoUser)
        {
            user = nodeInfoUser;
        }
        var metrics = FirstObject(
            Find(envelopePayload, "deviceMetrics", "device_metrics", "telemetry"),
            Find(envelopePayload, "environmentMetrics", "environment_metrics"),
            nodeInfo is null ? null : Find(nodeInfo.Value, "deviceMetrics", "device_metrics", "telemetry"),
            Find(decoded, "telemetry"));

        var type = FirstNonBlank(String(root, "type"), String(packet, "type"), String(decoded, "portnum", "portNum"));
        var longName = FirstNonBlank(
            String(user, "longName", "long_name", "longname", "name"),
            nodeInfo is null ? null : String(nodeInfo.Value, "longName", "long_name", "longname", "name"),
            String(root, "longName", "long_name", "longname"));
        var shortName = FirstNonBlank(
            String(user, "shortName", "short_name", "shortname"),
            nodeInfo is null ? null : String(nodeInfo.Value, "shortName", "short_name", "shortname"),
            String(root, "shortName", "short_name", "shortname"));
        var hardwareModel = FirstNonBlank(
            String(user, "hwModel", "hw_model", "hardwareModel"),
            nodeInfo is null ? null : String(nodeInfo.Value, "hwModel", "hw_model", "hardwareModel"),
            String(envelopePayload, "hwModel", "hw_model", "hardwareModel"));
        var role = FirstNonBlank(
            String(user, "role"),
            nodeInfo is null ? null : String(nodeInfo.Value, "role"),
            String(envelopePayload, "role"));

        var hasProfileData =
            !string.IsNullOrWhiteSpace(longName) ||
            !string.IsNullOrWhiteSpace(shortName) ||
            !string.IsNullOrWhiteSpace(hardwareModel) ||
            !string.IsNullOrWhiteSpace(role) ||
            metrics is not null ||
            IsProfileMessageType(type);
        if (!hasProfileData) return null;

        var externalId = FirstNonBlank(
            NodeId(String(user, "id", "nodeId", "num")),
            nodeInfo is null ? null : NodeId(String(nodeInfo.Value, "id", "nodeId", "num")),
            NodeId(String(envelopePayload, "id", "nodeId", "from")),
            NodeId(String(decoded, "fromId", "from")),
            NodeId(String(packet, "fromId", "from")),
            NodeId(String(root, "fromId", "from")),
            NodeId(String(root, "sender")));
        if (string.IsNullOrWhiteSpace(externalId)) return null;

        var label = FirstNonBlank(longName, shortName);
        var metadata = new Dictionary<string, object?>
        {
            ["adapter"] = "meshtastic",
            ["messageType"] = type,
            ["sender"] = String(root, "sender"),
            ["channel"] = String(root, "channel"),
            ["nodeInfo"] = JsonObjectOrNull(user),
            ["envelopeNodeInfo"] = nodeInfo is null ? null : JsonObjectOrNull(nodeInfo.Value),
            ["metrics"] = metrics is null ? null : JsonObjectOrNull(metrics.Value)
        };

        var tags = string.Join(", ", new[] { "meshtastic", type, hardwareModel, role }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        return new ProviderDeviceProfile(
            externalId,
            ObservedAt(root, envelopePayload),
            label,
            longName,
            shortName,
            hardwareModel,
            role,
            string.IsNullOrWhiteSpace(tags) ? null : tags,
            Metadata: JsonSerializer.Serialize(metadata));
    }

    private static string PrettyJson(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return payload;
        }
    }

    private static JsonElement? Find(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        foreach (var name in names)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    private static JsonElement? FirstObject(params JsonElement?[] values)
        => values.FirstOrDefault(value => value is { ValueKind: JsonValueKind.Object });

    private static string? String(JsonElement element, params string[] names)
    {
        var value = Find(element, names);
        if (value is null) return null;
        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static double? Coordinate(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Find(element, name);
            if (value is null) continue;

            if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var number))
            {
                return name.Contains("_i", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("I", StringComparison.Ordinal)
                    ? number / 10_000_000d
                    : number;
            }

            if (value.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.Value.GetString(), out var parsed))
            {
                return name.Contains("_i", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("I", StringComparison.Ordinal)
                    ? parsed / 10_000_000d
                    : parsed;
            }
        }

        return null;
    }

    private static string DisplayType(string? type)
        => string.IsNullOrWhiteSpace(type) ? "(empty/control)" : type;

    private static bool IsProfileMessageType(string? type)
        => string.Equals(type, "nodeinfo", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, "node_info", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, "user", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, "telemetry", StringComparison.OrdinalIgnoreCase);

    private static object? JsonObjectOrNull(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<object?>(element.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ObservedAt(JsonElement root, JsonElement envelopePayload)
    {
        foreach (var value in new[] { String(envelopePayload, "time", "timestamp"), String(root, "rxTime", "timestamp") })
        {
            if (long.TryParse(value, out var unixSeconds))
            {
                var utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                return utc <= DateTime.UtcNow.AddMinutes(1) ? utc : DateTime.UtcNow;
            }
        }

        return null;
    }

    private static string? NodeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.StartsWith('!')) return trimmed;
        return ulong.TryParse(trimmed, out var nodeNumber) ? $"!{unchecked((uint)nodeNumber):x8}" : trimmed;
    }

    private static string PositionState(string? type, bool hasLatitude, bool hasLongitude, double? latitude, double? longitude)
    {
        if (!hasLatitude && !hasLongitude)
        {
            return string.IsNullOrWhiteSpace(type) ? "no-position-control" : "no-position";
        }

        if (!hasLatitude || !hasLongitude)
        {
            return "partial-position";
        }

        if (latitude is null || longitude is null)
        {
            return "unreadable-position";
        }

        if (PositionSanityFilter.IsNullIslandNoise(latitude.Value, longitude.Value))
        {
            return "null-island-request-or-no-fix";
        }

        return "usable-position";
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string FormatNodeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(none)";
        var trimmed = value.Trim();
        if (trimmed.StartsWith('!')) return trimmed;
        return ulong.TryParse(trimmed, out var nodeNumber) ? $"{trimmed}/!{unchecked((uint)nodeNumber):x8}" : trimmed;
    }
}
