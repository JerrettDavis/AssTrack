using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using FluentAssertions;

namespace AssTrack.Tests.Api;

public class SystemStatusTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SystemStatusTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSystemStatus_WithApiKey_Returns200()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSystemStatus_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSystemStatus_SimulationEnabled_IsBool()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        ((object)result!.SimulationEnabled).Should().BeOfType<bool>();
    }

    [Fact]
    public async Task GetSystemStatus_DatabaseProvider_IsSQLite()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.DatabaseProvider.Should().Be("SQLite");
    }

    [Fact]
    public async Task GetSystemStatus_RateLimitPermitLimit_IsPositive()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.RateLimitPermitLimit.Should().BeGreaterThan(0);
    }
}
