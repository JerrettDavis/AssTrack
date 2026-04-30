using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class SeedApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SeedApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Seed_CreatesAssets_Devices_Geofences()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        result.Should().NotBeNull();
        result!.AssetsCreated.Should().Be(3);
        result.DevicesCreated.Should().Be(3);
        result.GeofencesCreated.Should().Be(2);
        result.AlreadySeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Seed_IsIdempotent_ReturnAlreadySeeded_OnSecondCall()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        result.Should().NotBeNull();
        result!.AlreadySeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Seed_WithReset_WipesAndReseeds()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        result.Should().NotBeNull();
        result!.AlreadySeeded.Should().BeFalse();
        result.ResetPerformed.Should().BeTrue();
        result.AssetsCreated.Should().Be(3);
    }

    [Fact]
    public async Task Seed_Reset_DoesNotTouch_NonSeededRecords()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Manual Asset", null, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: true));

        var assetsResponse = await client.GetAsync("/api/assets");
        assetsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var assets = await assetsResponse.Content.ReadFromJsonAsync<AssetDto[]>();
        assets.Should().NotBeNull();
        assets!.Should().Contain(a => a.Name == "Manual Asset");
        assets.Should().ContainSingle(a => a.Name == "Fleet Van Alpha" && a.IsSeeded);
    }

    [Fact]
    public async Task Seed_WhenSimulationDisabled_Returns403()
    {
        await _factory.ResetDatabaseAsync();
        using var disabledFactory = new DisabledSeedWebApplicationFactory();
        using var client = disabledFactory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_HasData_IsFalse_WhenNoAssets()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        status.Should().NotBeNull();
        status!.HasData.Should().BeFalse();
    }

    [Fact]
    public async Task Status_HasData_IsTrue_AfterSeed()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        status.Should().NotBeNull();
        status!.HasData.Should().BeTrue();
    }
}

file sealed class DisabledSeedWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:Enabled"] = "false"
            });
        });
    }
}
