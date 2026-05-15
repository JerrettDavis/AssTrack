using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.BridgeGateway;
using AssTrack.BridgeGateway.Adapters;
using AssTrack.BridgeGateway.Endpoints;
using AssTrack.BridgeGateway.Services;
using AssTrack.Domain.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssTrack.Tests.Api;

public sealed class BridgeGatewayTests
{
    private static readonly Guid FeedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Normalized_adapter_maps_bridge_payload_with_feed_defaults()
    {
        var adapter = new NormalizedJsonAdapter();
        using var document = JsonDocument.Parse("""
        {
          "externalTrackerId": " gps-17 ",
          "observedAt": "2026-05-06T01:30:00Z",
          "latitude": 41.8781,
          "longitude": -87.6298,
          "accuracyMeters": 11,
          "speedKmh": 64,
          "headingDegrees": 270,
          "label": "Truck GPS",
          "tags": "primary"
        }
        """);

        var observations = await adapter.ParseAsync(Context("gps-http", defaultTags: "gps"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        var observation = observations[0];
        observation.ExternalTrackerId.Should().Be("gps-17");
        observation.Latitude.Should().Be(41.8781);
        observation.Longitude.Should().Be(-87.6298);
        observation.Tags.Should().Be("gps, primary");
        observation.Metadata.Should().Contain("normalized-json");
    }

    [Fact]
    public async Task OwnTracks_adapter_maps_http_payload()
    {
        var adapter = new OwnTracksAdapter();
        using var document = JsonDocument.Parse("""
        {
          "_type": "location",
          "topic": "owntracks/jd/phone",
          "lat": 40.1,
          "lon": -86.2,
          "tst": 1778029200,
          "acc": 9,
          "vel": 38,
          "cog": 180,
          "batt": 83
        }
        """);

        var observations = await adapter.ParseAsync(Context("owntracks"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("owntracks/jd/phone");
        observations[0].SpeedKmh.Should().Be(38);
        observations[0].Tags.Should().Contain("owntracks");
        observations[0].Metadata.Should().Contain("battery");
    }

    [Fact]
    public async Task Meshtastic_adapter_maps_mqtt_json_position()
    {
        var adapter = new MeshtasticAdapter();
        using var document = JsonDocument.Parse("""
        {
          "fromId": "!abc123",
          "longName": "Field Node",
          "packet": {
            "decoded": {
              "position": {
                "latitudeI": 418781000,
                "longitudeI": -876298000,
                "altitude": 182,
                "groundSpeed": 12.5,
                "groundTrack": 90
              }
            }
          }
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("!abc123");
        observations[0].Latitude.Should().BeApproximately(41.8781, 0.00001);
        observations[0].Longitude.Should().BeApproximately(-87.6298, 0.00001);
        observations[0].SpeedKmh.Should().Be(45);
    }

    [Fact]
    public async Task Meshtastic_adapter_maps_documented_json_payload_envelope()
    {
        var adapter = new MeshtasticAdapter();
        using var document = JsonDocument.Parse("""
        {
          "id": 452664779,
          "channel": 1,
          "from": 2130636288,
          "payload": {
            "latitude_i": 418781000,
            "longitude_i": -876298000,
            "altitude": 184,
            "precision_bits": 32,
            "PDOP": 169,
            "sats_in_view": 10,
            "time": 1778039200
          },
          "sender": "!7efeee00",
          "timestamp": 1778039201,
          "to": -1,
          "type": "position"
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("!7efeee00");
        observations[0].Latitude.Should().BeApproximately(41.8781, 0.00001);
        observations[0].Longitude.Should().BeApproximately(-87.6298, 0.00001);
        observations[0].Altitude.Should().Be(184);
        observations[0].AccuracyMeters.Should().Be(1.6);
        observations[0].Metadata.Should().Contain("precisionBits");
        observations[0].Metadata.Should().Contain("satsInView");
    }

    [Fact]
    public async Task Meshtastic_adapter_estimates_accuracy_from_lower_precision_bits()
    {
        var adapter = new MeshtasticAdapter();
        using var document = JsonDocument.Parse("""
        {
          "from": "!privacy-node",
          "payload": {
            "latitude_i": 418781000,
            "longitude_i": -876298000,
            "precision_bits": 24
          },
          "type": "position"
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].AccuracyMeters.Should().BeGreaterThan(2.5);
    }

    [Fact]
    public async Task Meshtastic_adapter_uses_packet_originator_before_mqtt_gateway_sender()
    {
        var adapter = new MeshtasticAdapter();
        using var document = JsonDocument.Parse("""
        {
          "from": "!child-node",
          "sender": "!base-station",
          "payload": {
            "latitude_i": 418781000,
            "longitude_i": -876298000,
            "altitude": 184,
            "time": 1778039200
          },
          "type": "position"
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("!child-node");
        observations[0].Metadata.Should().Contain("!base-station");
    }

    [Fact]
    public async Task Meshtastic_adapter_normalizes_numeric_originator_to_node_id()
    {
        var adapter = new MeshtasticAdapter();
        using var document = JsonDocument.Parse("""
        {
          "from": 2130636288,
          "sender": "!base-station",
          "payload": {
            "latitude_i": 418781000,
            "longitude_i": -876298000
          },
          "type": "position"
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("!7efeee00");
    }

    [Fact]
    public async Task Meshtastic_adapter_clamps_future_device_time_to_gateway_time()
    {
        var adapter = new MeshtasticAdapter();
        var gatewayTime = DateTime.UtcNow.AddSeconds(-30);
        var gatewayTimestamp = new DateTimeOffset(gatewayTime).ToUnixTimeSeconds();
        var futureDeviceTime = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds();
        using var document = JsonDocument.Parse($$"""
        {
          "from": "!child-node",
          "timestamp": {{gatewayTimestamp}},
          "payload": {
            "latitude_i": 418781000,
            "longitude_i": -876298000,
            "time": {{futureDeviceTime}}
          },
          "type": "position"
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ObservedAt.Should().BeCloseTo(gatewayTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Meshtastic_adapter_parses_null_island_position_requests_as_observations_for_gateway_filtering()
    {
        var adapter = new MeshtasticAdapter();
        using var document = JsonDocument.Parse("""
        {
          "channel": 0,
          "from": 4134541604,
          "payload": {
            "latitude_i": 0,
            "longitude_i": 0
          },
          "sender": "!f6701924",
          "timestamp": 1778123347,
          "to": 318045044,
          "type": "position"
        }
        """);

        var observations = await adapter.ParseAsync(Context("meshtastic"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("!f6701924");
        observations[0].Latitude.Should().Be(0);
        observations[0].Longitude.Should().Be(0);
    }

    [Fact]
    public async Task Traccar_adapter_maps_event_position()
    {
        var adapter = new TraccarAdapter();
        using var document = JsonDocument.Parse("""
        {
          "type": "deviceOnline",
          "device": { "id": 42, "uniqueId": "imei-42", "name": "Truck 42" },
          "position": {
            "id": 99,
            "fixTime": "2026-05-06T02:00:00Z",
            "latitude": 39.1,
            "longitude": -88.2,
            "speed": 10,
            "course": 12
          }
        }
        """);

        var observations = await adapter.ParseAsync(Context("traccar"), document.RootElement, CancellationToken.None);

        observations.Should().ContainSingle();
        observations[0].ExternalTrackerId.Should().Be("imei-42");
        observations[0].Label.Should().Be("Truck 42");
        observations[0].SpeedKmh.Should().BeApproximately(18.52, 0.001);
    }

    [Fact]
    public async Task Bridge_handler_validates_shared_secret_and_dry_runs()
    {
        var handler = new BridgeRequestHandler(
            Options.Create(new BridgeGatewayOptions
            {
                DryRun = true,
                Feeds =
                {
                    ["generic"] = new BridgeFeedOptions
                    {
                        Enabled = true,
                        FeedId = FeedId,
                        Provider = "generic-webhook",
                        SharedSecret = "secret",
                        DefaultTags = "bridge"
                    }
                }
            }),
            new DynamicBridgeFeedStore(),
            new ProviderAdapterRegistry([new NormalizedJsonAdapter()]),
            new FakeIngestClient(),
            new BridgeFeedMonitor());

        using var document = JsonDocument.Parse("""{"externalTrackerId":"x1","observedAt":"2026-05-06T01:30:00Z","latitude":1,"longitude":2}""");

        var rejected = await handler.HandleAsync("generic", "wrong", document.RootElement, CancellationToken.None);
        rejected.Deliveries[0].StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var accepted = await handler.HandleAsync("generic", "secret", document.RootElement, CancellationToken.None);
        accepted.DryRun.Should().BeTrue();
        accepted.ObservationsReceived.Should().Be(1);
        accepted.ParsedObservations.Should().ContainSingle(x => x.Tags == "bridge");
    }

    [Fact]
    public async Task Bridge_handler_ignores_null_island_observations_before_delivery()
    {
        var ingestClient = new FakeIngestClient();
        var handler = new BridgeRequestHandler(
            Options.Create(new BridgeGatewayOptions
            {
                Feeds =
                {
                    ["generic"] = new BridgeFeedOptions
                    {
                        Enabled = true,
                        FeedId = FeedId,
                        Provider = "generic-webhook",
                        SharedSecret = "secret"
                    }
                }
            }),
            new DynamicBridgeFeedStore(),
            new ProviderAdapterRegistry([new NormalizedJsonAdapter()]),
            ingestClient,
            new BridgeFeedMonitor());

        using var document = JsonDocument.Parse("""{"externalTrackerId":"x1","observedAt":"2026-05-06T01:30:00Z","latitude":0.00001,"longitude":0.00001}""");

        var result = await handler.HandleAsync("generic", "secret", document.RootElement, CancellationToken.None);

        result.ObservationsReceived.Should().Be(1);
        result.Deliveries.Should().BeEmpty();
        ingestClient.SendCount.Should().Be(0);
    }

    [Fact]
    public async Task AssTrack_ingest_client_posts_gateway_payload_to_real_api_contract()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.ResetDatabaseAsync();

        var operatorClient = factory.CreateAuthenticatedClient();
        var createFeed = await operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            Name = "Gateway Contract",
            Provider = "generic-webhook",
            IsEnabled = true,
            AutoCreateDevices = true,
            DefaultTags = "contract"
        });
        createFeed.EnsureSuccessStatusCode();
        var feed = await createFeed.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var client = factory.CreateClient();
        var ingestClient = new AssTrackIngestClient(client, Options.Create(new BridgeGatewayOptions
        {
            AssTrackBaseUrl = client.BaseAddress,
            IngestApiKey = TestWebApplicationFactory.TestIngestApiKey
        }));

        var result = await ingestClient.SendAsync(feedId, new ProviderObservation(
            "gateway-device-1",
            DateTime.Parse("2026-05-06T01:30:00Z").ToUniversalTime(),
            41.1,
            -87.2,
            AccuracyMeters: 8,
            Label: "Gateway Device",
            Tags: "gateway",
            Metadata: """{"source":"bridge-test"}"""), CancellationToken.None);

        result.Success.Should().BeTrue(result.ResponseBody);

        var devices = await operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        devices.Should().ContainSingle(device => device.GetProperty("externalId").GetString() == "gateway-device-1");
    }

    [Fact]
    public async Task AssTrack_ingest_client_hands_off_bridge_messages_to_real_api_contract()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.ResetDatabaseAsync();

        var operatorClient = factory.CreateAuthenticatedClient();
        var createFeed = await operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            Name = "Signal Bridge",
            Provider = "signal",
            IsEnabled = true,
            AutoCreateDevices = false,
            ConfigurationJson = """{"bridgeKey":"signal-local","sharedSecret":"bridge-secret"}"""
        });
        createFeed.EnsureSuccessStatusCode();
        var feed = await createFeed.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var client = factory.CreateClient();
        var ingestClient = new AssTrackIngestClient(client, Options.Create(new BridgeGatewayOptions
        {
            AssTrackBaseUrl = client.BaseAddress,
            IngestApiKey = TestWebApplicationFactory.TestIngestApiKey
        }));

        var inbound = await ingestClient.SendInboundMessageAsync(new InboundMessageRequest(
            "direct",
            "signal",
            feedId,
            DeviceId: null,
            AssetId: null,
            ExternalPeerId: "+15551234567",
            DisplayName: "Field Lead",
            Sender: "+15551234567",
            Body: "Gate is open",
            ProviderMessageId: "signal-in-1",
            ReceivedAt: DateTime.UtcNow,
            Metadata: """{"source":"bridge"}"""), CancellationToken.None);

        inbound.Success.Should().BeTrue(inbound.ResponseBody);

        var threads = await operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/messages/threads");
        var thread = threads.Should().ContainSingle(item => item.GetProperty("externalPeerId").GetString() == "+15551234567").Subject;
        var threadId = thread.GetProperty("id").GetGuid();

        var sendResponse = await operatorClient.PostAsJsonAsync($"/api/messages/threads/{threadId}/messages", new
        {
            body = "Copy that"
        });
        sendResponse.EnsureSuccessStatusCode();
        var queued = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = queued.GetProperty("id").GetGuid();

        var outbound = await ingestClient.GetOutboundMessagesAsync(feedId, 10, CancellationToken.None);
        outbound.Success.Should().BeTrue(outbound.ResponseBody);
        outbound.Messages.Should().ContainSingle(message => message.Id == messageId && message.Body == "Copy that");

        var status = await ingestClient.UpdateMessageStatusAsync(messageId, new UpdateMessageStatusRequest(
            "sent",
            "signal-out-1",
            DateTime.UtcNow,
            ErrorMessage: null), CancellationToken.None);

        status.Success.Should().BeTrue(status.ResponseBody);

        var entries = await operatorClient.GetFromJsonAsync<List<JsonElement>>($"/api/messages/threads/{threadId}/messages");
        entries.Should().Contain(message =>
            message.GetProperty("id").GetGuid() == messageId &&
            message.GetProperty("status").GetString() == "sent" &&
            message.GetProperty("providerMessageId").GetString() == "signal-out-1");
    }

    [Fact]
    public async Task Bridge_gateway_message_endpoints_validate_secret_and_forward_to_ingest_client()
    {
        var ingestClient = new FakeIngestClient
        {
            OutboundMessages =
            [
                new OutboundMessageDto(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    FeedId,
                    "direct",
                    "signal",
                    "+15551234567",
                    "Field Lead",
                    "+15551234567",
                    "Copy that",
                    null,
                    DateTime.UtcNow)
            ]
        };

        await using var app = BuildGatewayTestApp(ingestClient);
        await app.StartAsync();
        var client = app.GetTestClient();

        var rejected = await client.PostAsJsonAsync("/bridge/signal/messages/inbound", new
        {
            externalPeerId = "+15551234567",
            body = "Gate is open"
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var inbound = await client.PostAsJsonAsync("/bridge/signal/messages/inbound?secret=bridge-secret", new
        {
            externalPeerId = "+15551234567",
            body = "Gate is open",
            sender = "+15551234567",
            providerMessageId = "signal-in-1"
        });
        inbound.StatusCode.Should().Be(HttpStatusCode.Accepted);
        ingestClient.InboundMessage.Should().NotBeNull();
        ingestClient.InboundMessage!.Provider.Should().Be("signal");
        ingestClient.InboundMessage.IntegrationFeedId.Should().Be(FeedId);
        ingestClient.InboundMessage.ExternalPeerId.Should().Be("+15551234567");

        var outbound = await client.GetFromJsonAsync<List<OutboundMessageDto>>("/bridge/signal/messages/outbound?secret=bridge-secret");
        outbound.Should().ContainSingle(message => message.Body == "Copy that");

        var status = await client.PostAsJsonAsync("/bridge/signal/messages/22222222-2222-2222-2222-222222222222/status?secret=bridge-secret", new
        {
            status = "delivered",
            providerMessageId = "signal-out-1",
            sentAt = DateTime.UtcNow
        });
        status.StatusCode.Should().Be(HttpStatusCode.Accepted);
        ingestClient.StatusMessageId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        ingestClient.StatusUpdate!.Status.Should().Be("delivered");
    }

    [Fact]
    public void Dynamic_feed_store_maps_configuration_and_replaces_snapshot_case_insensitively()
    {
        var store = new DynamicBridgeFeedStore();
        var feedId = Guid.NewGuid();

        store.Replace([
            new BridgeIntegrationFeedConfigDto(
                feedId,
                "Field MQTT",
                "meshtastic",
                true,
                true,
                "mesh,field",
                """
                {
                  "bridgeKey": "Field-Mesh",
                  "bridgeEnabled": true,
                  "sharedSecret": "secret",
                  "topics": ["msh/US/#", "msh/EU/#"],
                  "port": 1883
                }
                """,
                DateTime.UtcNow)
        ]);

        store.TryGet("field-mesh", out var feed).Should().BeTrue();
        feed.FeedId.Should().Be(feedId);
        feed.Enabled.Should().BeTrue();
        feed.SharedSecret.Should().Be("secret");
        feed.DefaultTags.Should().Be("mesh,field");
        feed.Settings["topics"].Should().Be("msh/US/#,msh/EU/#");
        feed.Settings["port"].Should().Be("1883");

        store.Replace([
            new BridgeIntegrationFeedConfigDto(
                Guid.NewGuid(),
                "Replacement Feed",
                "generic-webhook",
                true,
                false,
                null,
                """{"bridgeEnabled":false}""",
                DateTime.UtcNow)
        ]);

        store.TryGet("field-mesh", out _).Should().BeFalse();
        store.TryGet("replacement-feed", out var replacement).Should().BeTrue();
        replacement.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Dynamic_feed_store_tolerates_invalid_configuration_json()
    {
        var store = new DynamicBridgeFeedStore();
        var feedId = Guid.NewGuid();

        store.Replace([
            new BridgeIntegrationFeedConfigDto(
                feedId,
                "Invalid Json Feed",
                "generic-webhook",
                true,
                false,
                null,
                "{not-json",
                DateTime.UtcNow)
        ]);

        store.TryGet("invalid-json-feed", out var feed).Should().BeTrue();
        feed.FeedId.Should().Be(feedId);
        feed.Settings.Should().BeEmpty();
    }

    [Fact]
    public void Bridge_feed_monitor_filters_logs_messages_and_tracks_resyncs()
    {
        var monitor = new BridgeFeedMonitor();
        monitor.Update("mesh", status =>
        {
            status.Provider = "meshtastic";
            status.State = "connected";
        });
        monitor.Log("mesh", "info", "Connected to MQTT");
        monitor.Log("other", "warn", "Other feed warning");
        monitor.RecordMessage("mesh", "meshtastic", "msh/US/2/json/LongFast", "!abc123", "position", "Position packet", """{"lat":1}""");
        monitor.RecordMessage("mesh", "meshtastic", "msh/US/2/json/LongFast", "!def456", "text", "Text packet", """{"text":"hello"}""");

        monitor.Statuses.Should().ContainSingle(status => status.FeedKey == "mesh" && status.State == "connected");
        monitor.Logs("mesh").Should().ContainSingle(log => log.Message == "Connected to MQTT");
        monitor.Messages(feedKey: "mesh", trackerId: "abc", limit: 10).Should().ContainSingle(message => message.TrackerId == "!abc123");
        monitor.Messages(search: "hello", payloadOnly: true).Should().ContainSingle(message => message.MessageType == "text");

        monitor.RequestResync("mesh").Should().Be(1);
        monitor.RequestResync("mesh").Should().Be(2);
        monitor.GetResyncVersion("mesh").Should().Be(2);
        monitor.Statuses.Single(status => status.FeedKey == "mesh").State.Should().Be("resync-requested");
    }

    [Fact]
    public async Task Bridge_gateway_feeds_endpoint_merges_dynamic_feeds_before_static_feeds()
    {
        var dynamicStore = new DynamicBridgeFeedStore();
        var dynamicFeedId = Guid.NewGuid();
        dynamicStore.Replace([
            new BridgeIntegrationFeedConfigDto(
                dynamicFeedId,
                "Signal Dynamic",
                "signal",
                true,
                false,
                "dynamic",
                """{"bridgeKey":"signal","sharedSecret":"dynamic-secret"}""",
                DateTime.UtcNow)
        ]);

        await using var app = BuildGatewayTestApp(new FakeIngestClient(), dynamicStore);
        await app.StartAsync();

        var feeds = await app.GetTestClient().GetFromJsonAsync<List<BridgeFeedSummary>>("/bridge/feeds");

        feeds.Should().NotBeNull();
        feeds!.Should().ContainSingle(feed =>
            feed.Key == "signal" &&
            feed.FeedId == dynamicFeedId &&
            feed.Provider == "signal" &&
            feed.HasSharedSecret);
    }

    private static BridgeFeedContext Context(string provider, string? defaultTags = null)
        => new("test", new BridgeFeedOptions
        {
            Enabled = true,
            FeedId = FeedId,
            Provider = provider,
            DefaultTags = defaultTags
        }, provider);

    private static WebApplication BuildGatewayTestApp(IAssTrackIngestClient ingestClient, DynamicBridgeFeedStore? dynamicBridgeFeedStore = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IOptions<BridgeGatewayOptions>>(Options.Create(new BridgeGatewayOptions
        {
            Feeds =
            {
                ["signal"] = new BridgeFeedOptions
                {
                    Enabled = true,
                    FeedId = FeedId,
                    Provider = "signal",
                    SharedSecret = "bridge-secret"
                }
            }
        }));
        builder.Services.AddSingleton(dynamicBridgeFeedStore ?? new DynamicBridgeFeedStore());
        builder.Services.AddSingleton(new BridgeFeedMonitor());
        builder.Services.AddSingleton(new ProviderAdapterRegistry([new NormalizedJsonAdapter()]));
        builder.Services.AddSingleton(ingestClient);
        builder.Services.AddSingleton<BridgeRequestHandler>();

        var app = builder.Build();
        app.MapBridgeGatewayEndpoints();
        return app;
    }

    private sealed class FakeIngestClient : IAssTrackIngestClient
    {
        public int SendCount { get; private set; }
        public InboundMessageRequest? InboundMessage { get; private set; }
        public Guid? StatusMessageId { get; private set; }
        public UpdateMessageStatusRequest? StatusUpdate { get; private set; }
        public IReadOnlyList<OutboundMessageDto> OutboundMessages { get; init; } = [];

        public Task<BridgeDeliveryResult> SendAsync(Guid feedId, ProviderObservation observation, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new BridgeDeliveryResult(true, (int)HttpStatusCode.OK, "{}", false));
        }

        public Task<BridgeDeliveryResult> SendDeviceProfileAsync(Guid feedId, ProviderDeviceProfile profile, CancellationToken cancellationToken)
            => Task.FromResult(new BridgeDeliveryResult(true, (int)HttpStatusCode.OK, "{}", false));

        public Task<BridgeDeliveryResult> SendInboundMessageAsync(InboundMessageRequest message, CancellationToken cancellationToken)
        {
            InboundMessage = message;
            return Task.FromResult(new BridgeDeliveryResult(true, (int)HttpStatusCode.OK, "{}", false));
        }

        public Task<BridgeOutboundMessagesResult> GetOutboundMessagesAsync(Guid feedId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new BridgeOutboundMessagesResult(true, (int)HttpStatusCode.OK, "[]", false, OutboundMessages));

        public Task<BridgeDeliveryResult> UpdateMessageStatusAsync(Guid messageId, UpdateMessageStatusRequest status, CancellationToken cancellationToken)
        {
            StatusMessageId = messageId;
            StatusUpdate = status;
            return Task.FromResult(new BridgeDeliveryResult(true, (int)HttpStatusCode.OK, "{}", false));
        }
    }
}
