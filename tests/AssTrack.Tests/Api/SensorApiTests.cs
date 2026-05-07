using System.Net;
using System.Net.Http.Json;
using AssTrack.Api.Endpoints;
using AssTrack.Domain.Contracts;
using FluentAssertions;

namespace AssTrack.Tests.Api;

public class SensorApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SensorApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostSensorReading_WithAssetScope_Should_CreateReading()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Service Dog", "Working animal", "Dog", AssetClass: "pet"));
        assetResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        var observedAt = new DateTime(2026, 5, 7, 10, 15, 0, DateTimeKind.Local);
        var response = await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(
            asset!.Id,
            null,
            null,
            null,
            "temperature",
            "Harness temperature",
            21.4,
            null,
            "C",
            observedAt,
            """{"source":"unit-test"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await response.Content.ReadFromJsonAsync<SensorReadingDto>();
        reading.Should().NotBeNull();
        reading!.AssetId.Should().Be(asset.Id);
        reading.AssetName.Should().Be("Service Dog");
        reading.SensorType.Should().Be("temperature");
        reading.NumericValue.Should().Be(21.4);
    }

    [Fact]
    public async Task PostSensorReading_WithDeviceIdentifier_Should_InferAssetAndAppearOnAsset()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 12", null, "Van", AssetClass: "vehicle"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        var deviceResponse = await operatorClient.PostAsJsonAsync("/api/devices", new CreateDeviceRequest("OBD-12", "OBD Gateway", "mqtt", asset!.Id, "obd"));
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sensorResponse = await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(
            null,
            null,
            "OBD-12",
            null,
            "fuel-level",
            "Fuel",
            72,
            null,
            "%",
            DateTime.UtcNow,
            null));
        sensorResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var assets = await operatorClient.GetFromJsonAsync<List<AssetDto>>("/api/assets");
        var updated = assets.Should().ContainSingle(x => x.Id == asset.Id).Subject;
        updated.LatestSensorReadings.Should().ContainSingle(x => x.SensorType == "fuel_level" && x.NumericValue == 72);
    }

    [Fact]
    public async Task GetSensorReadings_Should_FilterBySearchScope()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Generator", null, "Equipment", AssetClass: "equipment"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(asset!.Id, null, null, null, "battery", null, 12.7, null, "V", DateTime.UtcNow, null));
        await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(asset.Id, null, null, null, "temperature", null, 33.2, null, "C", DateTime.UtcNow, null));

        var readings = await operatorClient.GetFromJsonAsync<List<SensorReadingDto>>($"/api/sensors/readings?assetId={asset.Id}&sensorType=battery");

        readings.Should().ContainSingle();
        readings![0].SensorType.Should().Be("battery");
        readings[0].NumericValue.Should().Be(12.7);
    }

    [Fact]
    public async Task PostSensorReading_WithoutValue_Returns422()
    {
        await _factory.ResetDatabaseAsync();
        using var ingestClient = _factory.CreateIngestClient();

        var response = await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(
            Guid.NewGuid(),
            null,
            null,
            null,
            "battery",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
