using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AssTrack.Tests.Api;

public class AuthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record IdentityResponse(
        [property: JsonPropertyName("roles")] string[] Roles,
        [property: JsonPropertyName("accessTier")] string AccessTier,
        [property: JsonPropertyName("capabilities")] string[] Capabilities);

    [Fact]
    public async Task AuthMe_WithOperatorKey_Returns200AndBothRoles()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IdentityResponse>();
        Assert.NotNull(result);
        Assert.Equal("enterprise", result.AccessTier);
        Assert.Contains("operator", result.Roles);
        Assert.Contains("admin", result.Roles);
        Assert.Contains("ingest", result.Roles);
        Assert.Contains("rbac", result.Capabilities);
    }

    [Fact]
    public async Task AuthMe_WithIngestKey_Returns200AndIngestIdentity()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IdentityResponse>();
        Assert.NotNull(result);
        Assert.Equal("device", result.AccessTier);
        Assert.Contains("ingest", result.Roles);
        Assert.DoesNotContain("operator", result.Roles);
        Assert.Contains("ingest", result.Capabilities);
    }

    [Fact]
    public async Task AuthMe_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithSeparateAdminKey_DistinguishesOperatorAndAdmin()
    {
        const string adminKey = "test-admin-key";
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = adminKey,
            ["Auth:AccessTier"] = "professional"
        });

        using var operatorClient = factory.CreateAuthenticatedClient();
        var operatorResponse = await operatorClient.GetFromJsonAsync<IdentityResponse>("/api/auth/me");
        Assert.NotNull(operatorResponse);
        Assert.Equal("professional", operatorResponse!.AccessTier);
        Assert.Contains("operator", operatorResponse.Roles);
        Assert.DoesNotContain("admin", operatorResponse.Roles);

        using var adminClient = factory.CreateClientWithApiKey(adminKey);
        var adminResponse = await adminClient.GetFromJsonAsync<IdentityResponse>("/api/auth/me");
        Assert.NotNull(adminResponse);
        Assert.Contains("admin", adminResponse!.Roles);
        Assert.Contains("manage-access", adminResponse.Capabilities);
    }
}
