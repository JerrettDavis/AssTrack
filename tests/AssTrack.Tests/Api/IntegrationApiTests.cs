using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AssTrack.Tests.Api;

public class IntegrationApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _operatorClient = factory.CreateAuthenticatedClient();
    private readonly HttpClient _ingestClient = factory.CreateIngestClient();

    [Fact]
    public async Task GetProviders_IncludesStandardLocationProviders()
    {
        var providers = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/integrations/providers");

        Assert.NotNull(providers);
        Assert.Contains(providers, p => p.GetProperty("id").GetString() == "apple-findmy");
        Assert.Contains(providers, p => p.GetProperty("id").GetString() == "google-findhub");
        Assert.Contains(providers, p => p.GetProperty("id").GetString() == "samsung-find");
        Assert.Contains(providers, p => p.GetProperty("id").GetString() == "meshtastic");
        Assert.Contains(providers, p => p.GetProperty("id").GetString() == "home-assistant");
    }

    [Fact]
    public async Task CreateFeed_ThenList_ReturnsConfiguredFeed()
    {
        var response = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Meshtastic mesh",
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true,
            defaultTags = "mesh, lora"
        });

        response.EnsureSuccessStatusCode();

        var feeds = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/integrations");

        Assert.NotNull(feeds);
        Assert.Contains(feeds, feed => feed.GetProperty("name").GetString() == "Meshtastic mesh");
    }

    [Fact]
    public async Task BridgeConfig_ReturnsGuiConfiguredProviderSettings()
    {
        var response = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Home Assistant",
            provider = "home-assistant",
            isEnabled = true,
            autoCreateDevices = true,
            defaultTags = "home-assistant",
            configurationJson = """
            {
              "bridgeKey": "ha-main",
              "sharedSecret": "secret",
              "baseUrl": "http://homeassistant.local:8123",
              "entities": ["device_tracker.phone"]
            }
            """
        });

        response.EnsureSuccessStatusCode();

        var feeds = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/integrations/bridge-config");

        Assert.NotNull(feeds);
        Assert.Contains(feeds, feed =>
            feed.GetProperty("provider").GetString() == "home-assistant" &&
            feed.GetProperty("configurationJson").GetString()?.Contains("ha-main") == true);
    }

    [Fact]
    public async Task FeedObservation_AutoCreatesDevice_AndStoresObservation()
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Generic feed",
            provider = "generic-webhook",
            isEnabled = true,
            autoCreateDevices = true,
            defaultTags = "gps"
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var observationResponse = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/observations", new
        {
            externalTrackerId = "tracker-001",
            observedAt = DateTime.UtcNow,
            latitude = 39.7392,
            longitude = -104.9903,
            speedKmh = 42,
            label = "Cell puck"
        });

        observationResponse.EnsureSuccessStatusCode();
        var result = await observationResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("deviceCreated").GetBoolean());
        Assert.Equal("generic-webhook:tracker-001", result.GetProperty("deviceIdentifier").GetString());

        var devices = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        Assert.NotNull(devices);
        Assert.Contains(devices, d =>
            d.GetProperty("provider").GetString() == "generic-webhook" &&
            d.GetProperty("externalId").GetString() == "tracker-001");
    }

    [Fact]
    public async Task FeedObservation_WhenAutoCreateDisabledAndUnknownTracker_ReturnsValidation()
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Locked feed",
            provider = "gps-http",
            isEnabled = true,
            autoCreateDevices = false
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var observationResponse = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/observations", new
        {
            externalTrackerId = "unknown",
            observedAt = DateTime.UtcNow,
            latitude = 39.7392,
            longitude = -104.9903
        });

        Assert.Equal(HttpStatusCode.BadRequest, observationResponse.StatusCode);
    }
}
