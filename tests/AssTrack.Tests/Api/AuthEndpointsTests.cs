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

    private record IdentityResponse([property: JsonPropertyName("roles")] string[] Roles);

    [Fact]
    public async Task AuthMe_WithOperatorKey_Returns200AndBothRoles()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IdentityResponse>();
        Assert.NotNull(result);
        Assert.Contains("operator", result.Roles);
        Assert.Contains("ingest", result.Roles);
    }

    [Fact]
    public async Task AuthMe_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
