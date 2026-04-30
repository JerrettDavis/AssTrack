using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AssTrack.Tests.Api;

/// <summary>
/// Proves that the ingest key is rejected (HTTP 403) from every operator-only endpoint.
/// Auth checks run before request-body parsing, so empty or minimal JSON bodies are fine.
/// </summary>
public class IngestKeyAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly Guid KnownGuid = new("00000000-0000-0000-0000-000000000001");

    private readonly TestWebApplicationFactory _factory;

    public IngestKeyAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -----------------------------------------------------------------------
    // Assets
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAssets_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/assets");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostAsset_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/assets", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutAsset_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PutAsJsonAsync($"/api/assets/{KnownGuid}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAsset_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.DeleteAsync($"/api/assets/{KnownGuid}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Devices
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDevices_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostDevice_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/devices", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutDevice_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PutAsJsonAsync($"/api/devices/{KnownGuid}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDevice_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.DeleteAsync($"/api/devices/{KnownGuid}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Geofences
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetGeofences_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/geofences");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostGeofence_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/geofences", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutGeofence_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PutAsJsonAsync($"/api/geofences/{KnownGuid}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGeofence_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.DeleteAsync($"/api/geofences/{KnownGuid}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Speed-alert acknowledgment
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AcknowledgeSpeedAlert_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync($"/api/speed-alerts/{KnownGuid}/acknowledge", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BulkAcknowledgeSpeedAlerts_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/speed-alerts/bulk-acknowledge", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Geofence-breach acknowledgment
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AcknowledgeGeofenceBreach_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync($"/api/geofences/breaches/{KnownGuid}/acknowledge", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BulkAcknowledgeGeofenceBreaches_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/geofences/breaches/bulk-acknowledge", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // SSE token issuance
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostEventsToken_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/events/token", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // System status (completeness — also in SystemStatusTests / AuthApiTests)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSystemStatus_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/system/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Positive: ingest key CAN post observations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostObservations_WithIngestKey_IsNotRejected()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/observations", new { });
        // Auth passes; body is invalid so expect 400/422, NOT 401/403.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostObservationsIngest_WithIngestKey_IsNotRejected()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/observations/ingest", new { });
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
