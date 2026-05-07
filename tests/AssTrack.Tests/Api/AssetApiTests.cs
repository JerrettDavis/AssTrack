using System.Net;
using System.Net.Http.Json;
using System.Text;
using AssTrack.Api.Endpoints;
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

        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 7", "Primary field vehicle", "Vehicle", AssetClass: "vehicle", Criticality: "high"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<AssetDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Fleet Van 7");
        created.AssetClass.Should().Be("vehicle");
        created.Criticality.Should().Be("high");

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
    public async Task GetAssetClasses_Should_ReturnCommercialClasses()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var classes = await client.GetFromJsonAsync<List<AssetClassDto>>("/api/assets/classes");

        classes.Should().NotBeNull();
        classes.Should().Contain(x => x.Id == "person");
        classes.Should().Contain(x => x.Id == "vehicle");
        classes.Should().Contain(x => x.Id == "property");
        classes.Should().Contain(x => x.Id == "pet");
    }

    [Fact]
    public async Task PostAsset_WithUnsupportedClass_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Unknown", null, null, AssetClass: "spaceship"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    [Fact]
    public async Task PostAsset_WithSpeedThreshold_ShouldPersistAndReturnThreshold()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Speedy Van", null, null, 75.0));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<AssetDto>();
        created.Should().NotBeNull();
        created!.SpeedThresholdKmh.Should().Be(75.0);

        var list = await client.GetFromJsonAsync<List<AssetDto>>("/api/assets");
        list.Should().Contain(x => x.Id == created.Id && x.SpeedThresholdKmh == 75.0);
    }

    [Fact]
    public async Task PostAsset_WithZeroSpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Test Van", null, null, 0.0));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAsset_WithNegativeSpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Test Van", null, null, -10.0));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutAsset_WithZeroSpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Test Van", null, null));
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        var update = await client.PutAsJsonAsync($"/api/assets/{created!.Id}", new UpdateAssetRequest("Test Van", null, null, 0.0));
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutAsset_WithNegativeSpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Test Van", null, null));
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        var update = await client.PutAsJsonAsync($"/api/assets/{created!.Id}", new UpdateAssetRequest("Test Van", null, null, -10.0));
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAsset_WithNaNSpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        // double.NaN cannot be serialized as valid JSON; send the string "NaN" to trigger binding failure → 400
        var content = new StringContent("""{"name":"Test Van","speedThresholdKmh":"NaN"}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/assets", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAsset_WithInfinitySpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        // double.PositiveInfinity cannot be serialized as valid JSON; send the string "Infinity" to trigger binding failure → 400
        var content = new StringContent("""{"name":"Test Van","speedThresholdKmh":"Infinity"}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/assets", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutAsset_WithNaNSpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Test Van", null, null));
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        // double.NaN cannot be serialized as valid JSON; send the string "NaN" to trigger binding failure → 400
        var content = new StringContent($$"""{"name":"Test Van","speedThresholdKmh":"NaN"}""", Encoding.UTF8, "application/json");
        var update = await client.PutAsync($"/api/assets/{created!.Id}", content);
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutAsset_WithInfinitySpeedThreshold_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Test Van", null, null));
        var created = await create.Content.ReadFromJsonAsync<AssetDto>();

        // double.NegativeInfinity cannot be serialized as valid JSON; send the string "-Infinity" to trigger binding failure → 400
        var content = new StringContent($$"""{"name":"Test Van","speedThresholdKmh":"-Infinity"}""", Encoding.UTF8, "application/json");
        var update = await client.PutAsync($"/api/assets/{created!.Id}", content);
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

