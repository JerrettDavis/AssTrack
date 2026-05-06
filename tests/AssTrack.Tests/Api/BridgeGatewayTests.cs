using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.BridgeGateway;
using AssTrack.BridgeGateway.Adapters;
using AssTrack.BridgeGateway.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

    private static BridgeFeedContext Context(string provider, string? defaultTags = null)
        => new("test", new BridgeFeedOptions
        {
            Enabled = true,
            FeedId = FeedId,
            Provider = provider,
            DefaultTags = defaultTags
        }, provider);

    private sealed class FakeIngestClient : IAssTrackIngestClient
    {
        public Task<BridgeDeliveryResult> SendAsync(Guid feedId, ProviderObservation observation, CancellationToken cancellationToken)
            => Task.FromResult(new BridgeDeliveryResult(true, (int)HttpStatusCode.OK, "{}", false));
    }
}
