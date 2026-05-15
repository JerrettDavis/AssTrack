using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using Xunit;

namespace AssTrack.Tests.Api;

public class AuthApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiEndpoint_WithValidApiKey_Returns200()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/assets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/assets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithWrongApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var response = await client.GetAsync("/api/assets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IngestEndpoint_WithOperatorKey_Returns201()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var asset = await CreateAssetAsync(client);
        var device = await CreateDeviceAsync(client, asset.Id);
        var response = await client.PostAsync("/api/observations", 
            new StringContent(
                $$"""{"deviceId":"{{device.Id}}","observedAt":"{{DateTime.UtcNow:O}}","latitude":51.5074,"longitude":-0.1278,"altitude":10,"accuracyMeters":5,"speedKmh":85}""",
                System.Text.Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task IngestKey_CanPost_Observations()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/observations", new { });
        // Should not be 401 or 403 (may be 400/422 due to bad body)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperatorOnlyEndpoint_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/system/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperatorOnlyEndpoint_WithOperatorKey_Returns200()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithSeparateOperatorKey_Returns403()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/system/maintenance/clean-null-island?dryRun=true", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithAdminKey_Returns200()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientWithApiKey("test-admin-key");

        var response = await client.PostAsync("/api/system/maintenance/clean-null-island?dryRun=true", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_WithoutApiKey_Returns200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiHealth_WithoutApiKey_Returns200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(Guid Id, string Identifier)> CreateAssetAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/assets",
            new StringContent(
                """{"name":"Test Asset","description":"Test","category":"Vehicle"}""",
                System.Text.Encoding.UTF8,
                "application/json"));
        var json = await response.Content.ReadFromJsonAsync<AssetDto>();
        return (json!.Id, json.Name);
    }

    private static async Task<(Guid Id, string Identifier)> CreateDeviceAsync(HttpClient client, Guid assetId)
    {
        var response = await client.PostAsync("/api/devices",
            new StringContent(
                $$"""{"identifier":"TEST-001","label":"Test Device","protocol":"https","assetId":"{{assetId}}"}""",
                System.Text.Encoding.UTF8,
                "application/json"));
        var json = await response.Content.ReadFromJsonAsync<DeviceDto>();
        return (json!.Id, json.Identifier);
    }
}
