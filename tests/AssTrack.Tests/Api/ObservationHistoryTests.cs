using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class ObservationHistoryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ObservationHistoryTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetObservationHistory_Should_ReturnPagedResult()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        Guid assetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Generator A", Category = "Equipment" };
            var device = new Device { Identifier = "dev-history-001", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
            assetId = asset.Id;

            var now = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                var observation = new Observation
                {
                    DeviceId = deviceId,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 40.7128 + i * 0.001,
                    Longitude = -74.0060 + i * 0.001,
                    SpeedKmh = 50 + i * 5
                };
                dbContext.Observations.Add(observation);
            }
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetObservationHistory_Should_FilterByDateRange()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        DateTime baseTime;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Generator B", Category = "Equipment" };
            var device = new Device { Identifier = "dev-history-002", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;

            baseTime = DateTime.UtcNow;
            var observations = new[]
            {
                new Observation { DeviceId = deviceId, ObservedAt = baseTime.AddMinutes(-30), ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 },
                new Observation { DeviceId = deviceId, ObservedAt = baseTime.AddMinutes(-20), ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 },
                new Observation { DeviceId = deviceId, ObservedAt = baseTime.AddMinutes(-10), ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 },
                new Observation { DeviceId = deviceId, ObservedAt = baseTime.AddMinutes(-5), ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 },
                new Observation { DeviceId = deviceId, ObservedAt = baseTime, ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 }
            };
            dbContext.Observations.AddRange(observations);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        // Use the same baseTime from above to ensure we're filtering correctly
        var from = baseTime.AddMinutes(-15).ToString("o");
        var to = baseTime.AddMinutes(5).ToString("o");
        
        var response = await client.GetAsync($"/api/observations/history?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        
        // Verify items are ordered by ObservedAt descending
        result.Items.Select(o => o.ObservedAt).Should().BeInDescendingOrder();
        
        // Verify we got the expected observations (the 3 within the range)
        // baseTime-10min, baseTime-5min, baseTime should be returned
        result.Items[0].ObservedAt.Should().BeCloseTo(baseTime, TimeSpan.FromSeconds(2));
        result.Items[1].ObservedAt.Should().BeCloseTo(baseTime.AddMinutes(-5), TimeSpan.FromSeconds(2));
        result.Items[2].ObservedAt.Should().BeCloseTo(baseTime.AddMinutes(-10), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetObservationHistory_Should_ReturnCsvContentType()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Generator C", Category = "Equipment" };
            var device = new Device { Identifier = "dev-history-003", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;

            var now = DateTime.UtcNow;
            for (int i = 0; i < 3; i++)
            {
                var observation = new Observation
                {
                    DeviceId = deviceId,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 40.7128,
                    Longitude = -74.0060,
                    SpeedKmh = 60
                };
                dbContext.Observations.Add(observation);
            }
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?deviceId={Uri.EscapeDataString(deviceId.ToString())}&format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("ObservationId,DeviceId,AssetId,ObservedAt,Latitude,Longitude,Altitude,SpeedKmh,Heading");
        csv.Should().Contain(deviceId.ToString());
    }

    [Fact]
    public async Task GetObservationHistory_Should_Return422_WhenCsvWithoutFilters()
    {
        await _factory.ResetDatabaseAsync();

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/observations/history?format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetObservationHistory_Should_FilterByAssetId()
    {
        await _factory.ResetDatabaseAsync();
        Guid assetId1;
        Guid assetId2;
        Guid deviceId1;
        Guid deviceId2;
        
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            
            var asset1 = new Asset { Name = "Asset 1", Category = "Equipment" };
            var asset2 = new Asset { Name = "Asset 2", Category = "Equipment" };
            dbContext.Assets.Add(asset1);
            dbContext.Assets.Add(asset2);
            await dbContext.SaveChangesAsync();
            assetId1 = asset1.Id;
            assetId2 = asset2.Id;

            var device1 = new Device { Identifier = "dev-asset-001", Asset = asset1 };
            var device2 = new Device { Identifier = "dev-asset-002", Asset = asset2 };
            dbContext.Devices.Add(device1);
            dbContext.Devices.Add(device2);
            await dbContext.SaveChangesAsync();
            deviceId1 = device1.Id;
            deviceId2 = device2.Id;

            var now = DateTime.UtcNow;
            for (int i = 0; i < 3; i++)
            {
                dbContext.Observations.Add(new Observation
                {
                    DeviceId = deviceId1,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 40.7128,
                    Longitude = -74.0060,
                    SpeedKmh = 50
                });
            }

            for (int i = 0; i < 2; i++)
            {
                dbContext.Observations.Add(new Observation
                {
                    DeviceId = deviceId2,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 51.5074,
                    Longitude = -0.1278,
                    SpeedKmh = 70
                });
            }
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?assetId={Uri.EscapeDataString(assetId1.ToString())}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Items.Should().AllSatisfy(o => o.AssetId.Should().Be(assetId1));
        result.Items.Should().AllSatisfy(o => o.AssetName.Should().Be("Asset 1"));
    }

    [Fact]
    public async Task GetObservationHistory_Should_FilterByDeviceId()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId1;
        Guid deviceId2;
        
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            
            var device1 = new Device { Identifier = "dev-filter-001" };
            var device2 = new Device { Identifier = "dev-filter-002" };
            dbContext.Devices.Add(device1);
            dbContext.Devices.Add(device2);
            await dbContext.SaveChangesAsync();
            deviceId1 = device1.Id;
            deviceId2 = device2.Id;

            var now = DateTime.UtcNow;
            for (int i = 0; i < 4; i++)
            {
                dbContext.Observations.Add(new Observation
                {
                    DeviceId = deviceId1,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 40.7128,
                    Longitude = -74.0060
                });
            }

            for (int i = 0; i < 2; i++)
            {
                dbContext.Observations.Add(new Observation
                {
                    DeviceId = deviceId2,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 51.5074,
                    Longitude = -0.1278
                });
            }
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?deviceId={Uri.EscapeDataString(deviceId1.ToString())}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(4);
        result.Items.Should().AllSatisfy(o => o.DeviceId.Should().Be(deviceId1));
        result.Items.Should().AllSatisfy(o => o.DeviceIdentifier.Should().Be("dev-filter-001"));
    }

    [Fact]
    public async Task GetObservationHistory_Should_HandlePagination()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-pagination-001" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;

            var now = DateTime.UtcNow;
            for (int i = 0; i < 25; i++)
            {
                dbContext.Observations.Add(new Observation
                {
                    DeviceId = deviceId,
                    ObservedAt = now.AddSeconds(-i * 10),
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 40.7128,
                    Longitude = -74.0060
                });
            }
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        
        var page1 = await client.GetAsync("/api/observations/history?page=1&pageSize=10");
        var result1 = await page1.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        result1!.Items.Should().HaveCount(10);
        result1.Page.Should().Be(1);
        result1.TotalCount.Should().Be(25);

        var page2 = await client.GetAsync("/api/observations/history?page=2&pageSize=10");
        var result2 = await page2.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        result2!.Items.Should().HaveCount(10);
        result2.Page.Should().Be(2);
        result2.TotalCount.Should().Be(25);

        var page3 = await client.GetAsync("/api/observations/history?page=3&pageSize=10");
        var result3 = await page3.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        result3!.Items.Should().HaveCount(5);
        result3.Page.Should().Be(3);
        result3.TotalCount.Should().Be(25);

        result1.Items.Select(o => o.Id).Should().NotIntersectWith(result2.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetObservationHistory_Should_IncludeDeviceAndAssetInfo()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        Guid assetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Fleet Vehicle", Category = "Vehicle" };
            var device = new Device { Identifier = "fleet-001", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
            assetId = asset.Id;

            var observation = new Observation
            {
                DeviceId = deviceId,
                ObservedAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Latitude = 40.7128,
                Longitude = -74.0060,
                SpeedKmh = 65
            };
            dbContext.Observations.Add(observation);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?deviceId={Uri.EscapeDataString(deviceId.ToString())}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result!.Items.Should().ContainSingle();
        var resultObservation = result.Items[0];
        resultObservation.DeviceId.Should().Be(deviceId);
        resultObservation.DeviceIdentifier.Should().Be("fleet-001");
        resultObservation.AssetId.Should().Be(assetId);
        resultObservation.AssetName.Should().Be("Fleet Vehicle");
        resultObservation.SpeedKmh.Should().Be(65);
    }

    [Fact]
    public async Task GetObservationHistory_Should_ReturnEmptyWhenNoMatches()
    {
        await _factory.ResetDatabaseAsync();

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?deviceId={Uri.EscapeDataString(Guid.NewGuid().ToString())}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetObservationHistory_Should_OrderByObservedAtDescending()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-order-001" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;

            var baseTime = DateTime.UtcNow;
            var times = new[] { baseTime.AddMinutes(-5), baseTime.AddMinutes(-2), baseTime.AddMinutes(-10), baseTime, baseTime.AddMinutes(-1) };
            foreach (var time in times)
            {
                dbContext.Observations.Add(new Observation
                {
                    DeviceId = deviceId,
                    ObservedAt = time,
                    ReceivedAt = DateTime.UtcNow,
                    Latitude = 40.7128,
                    Longitude = -74.0060
                });
            }
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/history?deviceId={Uri.EscapeDataString(deviceId.ToString())}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result!.Items.Should().HaveCount(5);
        
        for (int i = 1; i < result.Items.Count; i++)
        {
            result.Items[i - 1].ObservedAt.Should().BeOnOrAfter(result.Items[i].ObservedAt);
        }
    }

    [Fact]
    public async Task GetObservationHistory_Should_CombineMultipleFilters()
    {
        await _factory.ResetDatabaseAsync();
        Guid assetId;
        Guid deviceId1;
        Guid deviceId2;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Combined Asset", Category = "Equipment" };
            dbContext.Assets.Add(asset);
            await dbContext.SaveChangesAsync();
            assetId = asset.Id;

            var device1 = new Device { Identifier = "dev-combined-001", Asset = asset };
            var device2 = new Device { Identifier = "dev-combined-002", Asset = asset };
            dbContext.Devices.Add(device1);
            dbContext.Devices.Add(device2);
            await dbContext.SaveChangesAsync();
            deviceId1 = device1.Id;
            deviceId2 = device2.Id;

            var setupNow = DateTime.UtcNow;
            var withinRange = setupNow.AddMinutes(-5);
            var outsideRange = setupNow.AddMinutes(-100);

            dbContext.Observations.Add(new Observation { DeviceId = deviceId1, ObservedAt = withinRange, ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 });
            dbContext.Observations.Add(new Observation { DeviceId = deviceId1, ObservedAt = outsideRange, ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 });
            dbContext.Observations.Add(new Observation { DeviceId = deviceId2, ObservedAt = withinRange, ReceivedAt = DateTime.UtcNow, Latitude = 40.7128, Longitude = -74.0060 });

            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var now = DateTime.UtcNow;
        var from = now.AddMinutes(-10).ToString("o");
        var to = now.AddMinutes(5).ToString("o");
        var response = await client.GetAsync($"/api/observations/history?assetId={Uri.EscapeDataString(assetId.ToString())}&deviceId={Uri.EscapeDataString(deviceId1.ToString())}&from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ObservationDto>>();
        
        result!.Items.Should().HaveCount(1);
        result.Items[0].DeviceId.Should().Be(deviceId1);
        result.Items[0].AssetId.Should().Be(assetId);
    }
}
