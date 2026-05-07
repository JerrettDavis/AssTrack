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
    public async Task DeviceProfile_AutoCreatesDeviceWithoutAsset_ThenObservationUsesEnrichedLabel()
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Meshtastic profile feed",
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true,
            defaultTags = "mesh"
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var profileResponse = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/devices/profile", new
        {
            externalTrackerId = "!12f4fb75",
            longName = "Dev Tracker 001",
            shortName = "DT01",
            hardwareModel = "TRACKER_T1000_E",
            role = "CLIENT",
            tags = "t1000"
        });

        profileResponse.EnsureSuccessStatusCode();
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(profile.GetProperty("deviceCreated").GetBoolean());
        Assert.False(profile.GetProperty("assetCreated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("assetId").ValueKind);
        Assert.Equal("Dev Tracker 001", profile.GetProperty("label").GetString());

        var observationResponse = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/observations", new
        {
            externalTrackerId = "!12f4fb75",
            observedAt = DateTime.UtcNow,
            latitude = 36.0594,
            longitude = -95.8971
        });
        observationResponse.EnsureSuccessStatusCode();

        var devices = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        Assert.NotNull(devices);
        var device = Assert.Single(devices, d => d.GetProperty("externalId").GetString() == "!12f4fb75");
        Assert.Equal("Dev Tracker 001", device.GetProperty("label").GetString());
        Assert.Equal("Dev Tracker 001", device.GetProperty("providerLongName").GetString());
        Assert.Contains("TRACKER_T1000_E", device.GetProperty("tags").GetString());
        Assert.Contains("CLIENT", device.GetProperty("tags").GetString());
        Assert.Equal(JsonValueKind.Null, device.GetProperty("assetId").ValueKind);
    }

    [Fact]
    public async Task FeedObservation_WithLabel_AdditivelyUpdatesExistingDeviceProviderProfile()
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Meshtastic observation profile feed",
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var firstObservation = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/observations", new
        {
            externalTrackerId = "!12f4fb74",
            observedAt = DateTime.UtcNow,
            latitude = 36.0594,
            longitude = -95.8971
        });
        firstObservation.EnsureSuccessStatusCode();

        var labeledObservation = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/observations", new
        {
            externalTrackerId = "!12f4fb74",
            observedAt = DateTime.UtcNow.AddSeconds(1),
            latitude = 36.0595,
            longitude = -95.8972,
            label = "Dev Tracker 001"
        });
        labeledObservation.EnsureSuccessStatusCode();

        var devices = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        var device = Assert.Single(devices!, d => d.GetProperty("externalId").GetString() == "!12f4fb74");
        Assert.Equal("!12f4fb74", device.GetProperty("label").GetString());
        Assert.Equal("Dev Tracker 001", device.GetProperty("providerLabel").GetString());
    }

    [Fact]
    public async Task DeviceProfile_UpsertsProviderFieldsWithoutOverwritingLocalLabelOrAsset()
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Meshtastic masking feed",
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var firstProfile = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/devices/profile", new
        {
            externalTrackerId = "!mask-node",
            longName = "Provider Name 1",
            shortName = "PN1"
        });
        firstProfile.EnsureSuccessStatusCode();

        var devices = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        var created = Assert.Single(devices!, d => d.GetProperty("externalId").GetString() == "!mask-node");
        var deviceId = created.GetProperty("id").GetGuid();

        var assetResponse = await _operatorClient.PostAsJsonAsync("/api/assets", new
        {
            name = "Local Asset",
            description = "User enrolled asset",
            category = "Mesh node"
        });
        assetResponse.EnsureSuccessStatusCode();
        var asset = await assetResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assetId = asset.GetProperty("id").GetGuid();

        var updateDevice = await _operatorClient.PutAsJsonAsync($"/api/devices/{deviceId}", new
        {
            identifier = created.GetProperty("identifier").GetString(),
            label = "Local Mask",
            protocol = "meshtastic",
            assetId,
            provider = "meshtastic",
            externalId = "!mask-node",
            tags = "local",
            integrationFeedId = feedId
        });
        updateDevice.EnsureSuccessStatusCode();

        var updateAsset = await _operatorClient.PutAsJsonAsync($"/api/assets/{assetId}", new
        {
            name = "Local Asset",
            description = "User enrolled asset",
            category = "Mesh node",
            speedThresholdKmh = (double?)null
        });
        updateAsset.EnsureSuccessStatusCode();

        var secondProfile = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/devices/profile", new
        {
            externalTrackerId = "!mask-node",
            longName = "Provider Name 2",
            shortName = "PN2",
            hardwareModel = "HELTEC_V4",
            role = "CLIENT"
        });
        secondProfile.EnsureSuccessStatusCode();

        devices = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        var updated = Assert.Single(devices!, d => d.GetProperty("externalId").GetString() == "!mask-node");
        Assert.Equal("Local Mask", updated.GetProperty("label").GetString());
        Assert.Equal("Provider Name 2", updated.GetProperty("providerLongName").GetString());
        Assert.Equal("PN2", updated.GetProperty("providerShortName").GetString());
        Assert.Equal("HELTEC_V4", updated.GetProperty("providerHardwareModel").GetString());

        var assets = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/assets");
        Assert.Contains(assets!, asset => asset.GetProperty("id").GetGuid() == assetId && asset.GetProperty("name").GetString() == "Local Asset");
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

    [Fact]
    public async Task FeedObservation_WhenIngestValidationFails_ReturnsUnprocessableEntity()
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name = "Validation feed",
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var feedId = feed.GetProperty("id").GetGuid();

        var observationResponse = await _ingestClient.PostAsJsonAsync($"/api/integrations/{feedId}/observations", new
        {
            externalTrackerId = "!future-node",
            observedAt = DateTime.UtcNow.AddHours(1),
            latitude = 39.7392,
            longitude = -104.9903
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, observationResponse.StatusCode);
    }
}
