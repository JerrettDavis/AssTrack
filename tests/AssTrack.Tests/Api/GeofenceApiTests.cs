using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AssTrack.Tests.Api;

public class GeofenceApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GeofenceApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateGeofence_ValidData_Returns201()
    {
        var request = new
        {
            name = "Test Geofence",
            description = "A test geofence",
            centerLatitude = 37.7749,
            centerLongitude = -122.4194,
            radiusMeters = 500.0,
            isActive = true
        };
        var response = await _client.PostAsJsonAsync("/api/geofences", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateGeofence_ZeroRadius_Returns422()
    {
        var request = new
        {
            name = "Test Geofence",
            centerLatitude = 37.7749,
            centerLongitude = -122.4194,
            radiusMeters = 0.0,
            isActive = true
        };
        var response = await _client.PostAsJsonAsync("/api/geofences", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateGeofence_InvalidLatitude_Returns422()
    {
        var request = new
        {
            name = "Test Geofence",
            centerLatitude = 91.0,
            centerLongitude = 0.0,
            radiusMeters = 100.0,
            isActive = true
        };
        var response = await _client.PostAsJsonAsync("/api/geofences", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateGeofence_InvalidLongitude_Returns422()
    {
        var request = new
        {
            name = "Test Geofence",
            centerLatitude = 0.0,
            centerLongitude = 181.0,
            radiusMeters = 100.0,
            isActive = true
        };
        var response = await _client.PostAsJsonAsync("/api/geofences", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
