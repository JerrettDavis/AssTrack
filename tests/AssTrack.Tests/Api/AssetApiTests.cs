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

    [Fact]
    public async Task PutAsset_Should_UpdateAsset()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Old Name", null, null));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        var update = await client.PutAsJsonAsync($"/api/assets/{created!.Id}", new UpdateAssetRequest("New Name", "New Desc", "Equipment"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await update.Content.ReadFromJsonAsync<AssetDto>();
        updated!.Name.Should().Be("New Name");
        updated.Description.Should().Be("New Desc");
    }

    [Fact]
    public async Task PutAsset_NotFound_Returns404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var update = await client.PutAsJsonAsync($"/api/assets/{Guid.NewGuid()}", new UpdateAssetRequest("Name", null, null));
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsset_Should_Return204_AndBeGone()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("ToDelete", null, null));
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        var del = await client.DeleteAsync($"/api/assets/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/assets/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAssetById_Should_ReturnAsset()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("SingleAsset", "Desc", "Cat"));
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        var get = await client.GetFromJsonAsync<AssetDto>($"/api/assets/{created!.Id}");
        get!.Name.Should().Be("SingleAsset");
    }
}
