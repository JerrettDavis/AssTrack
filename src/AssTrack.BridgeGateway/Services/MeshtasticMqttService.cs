using System.Text;
using System.Text.Json;
using AssTrack.BridgeGateway.Adapters;
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
                monitor.Update(feedKey, status =>
                {
                    status.MessagesReceived++;
                    status.LastMessageAt = DateTime.UtcNow;
                    status.State = "receiving";
                    status.LastError = null;
                });
                using var document = JsonDocument.Parse(json);
                var observations = await adapter.ParseAsync(new BridgeFeedContext(feedKey, feed, "meshtastic"), document.RootElement, cancellationToken);
                monitor.Update(feedKey, status => status.ObservationsParsed += observations.Count);
                foreach (var observation in observations)
                {
                    monitor.Update(feedKey, status => status.LastTrackerId = observation.ExternalTrackerId);
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
                        monitor.Log(feedKey, "warn", $"Delivery failed for {observation.ExternalTrackerId}: {delivery.StatusCode}.");
                        logger.LogWarning("Meshtastic delivery failed for {TrackerId}: {StatusCode}", observation.ExternalTrackerId, delivery.StatusCode);
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
                monitor.Log(feedKey, "debug", $"Ignored MQTT payload: {ex.Message}");
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
}
