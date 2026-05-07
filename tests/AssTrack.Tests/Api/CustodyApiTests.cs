using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using FluentAssertions;

namespace AssTrack.Tests.Api;

public class CustodyApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CustodyApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostCustodyEvent_Should_CheckOutAsset_AndReturnHistory()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        var asset = await CreateAssetAsync(client);

        var response = await client.PostAsJsonAsync("/api/custody/events", new CreateCustodyEventRequest(
            asset.Id,
            "check_out",
            ToCustodianName: "Alex Rivera",
            ToCustodianContact: "alex@example.test",
            Location: "Yard A",
            Notes: "Issued for field work"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var custodyEvent = await response.Content.ReadFromJsonAsync<CustodyEventDto>();
        custodyEvent.Should().NotBeNull();
        custodyEvent!.EventType.Should().Be("check_out");
        custodyEvent.ToCustodianName.Should().Be("Alex Rivera");

        var updated = await client.GetFromJsonAsync<AssetDto>($"/api/assets/{asset.Id}");
        updated!.CustodyStatus.Should().Be("checked_out");
        updated.CustodianName.Should().Be("Alex Rivera");
        updated.CustodianContact.Should().Be("alex@example.test");
        updated.CustodySince.Should().NotBeNull();

        var history = await client.GetFromJsonAsync<List<CustodyEventDto>>($"/api/custody/events?assetId={asset.Id}");
        history.Should().ContainSingle(x => x.Id == custodyEvent.Id && x.Notes == "Issued for field work");
    }

    [Fact]
    public async Task PostCustodyEvent_CheckIn_Should_ClearCurrentCustodian()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        var asset = await CreateAssetAsync(client);

        await client.PostAsJsonAsync("/api/custody/events", new CreateCustodyEventRequest(asset.Id, "check_out", ToCustodianName: "Alex Rivera"));

        var response = await client.PostAsJsonAsync("/api/custody/events", new CreateCustodyEventRequest(asset.Id, "check_in"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var updated = await client.GetFromJsonAsync<AssetDto>($"/api/assets/{asset.Id}");
        updated!.CustodyStatus.Should().Be("available");
        updated.CustodianName.Should().BeNull();
        updated.CustodySince.Should().BeNull();
    }

    [Fact]
    public async Task PostCustodyEvent_CheckOutWithoutCustodian_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        var asset = await CreateAssetAsync(client);

        var response = await client.PostAsJsonAsync("/api/custody/events", new CreateCustodyEventRequest(asset.Id, "check_out"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCustodyEvent_WithUnsupportedStatus_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        var asset = await CreateAssetAsync(client);

        var response = await client.PostAsJsonAsync("/api/custody/events", new CreateCustodyEventRequest(asset.Id, "status_change", CustodyStatus: "unknown"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<AssetDto> CreateAssetAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Custody Trailer", null, "Trailer", AssetClass: "container"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await create.Content.ReadFromJsonAsync<AssetDto>())!;
    }
}
