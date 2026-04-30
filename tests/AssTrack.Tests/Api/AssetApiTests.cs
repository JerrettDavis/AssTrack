using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class AssetApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AssetApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostAsset_Should_CreateAsset_AndBeReturnedFromGet()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 7", "Primary field vehicle", "Vehicle"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<AssetDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Fleet Van 7");

        var list = await client.GetFromJsonAsync<List<AssetDto>>("/api/assets");
        list.Should().ContainSingle(x => x.Id == created.Id && x.Name == "Fleet Van 7");
    }

    [Fact]
    public async Task PostAsset_Should_PersistToDatabase()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Excavator", "Yard asset", "Equipment"));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var assets = await dbContext.Assets.ToListAsync();

        assets.Should().Contain(x => x.Name == "Excavator");
    }
}
