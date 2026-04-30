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

    [Fact]
    public async Task PutGeofence_Should_UpdateGeofence()
    {
        var create = await _client.PostAsJsonAsync("/api/geofences", new
        {
            name = "Original Geofence",
            centerLatitude = 10.0,
            centerLongitude = 20.0,
            radiusMeters = 100.0,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = created.GetProperty("id").GetString();

        var update = await _client.PutAsJsonAsync($"/api/geofences/{id}", new
        {
            name = "Updated Geofence",
            description = "New description",
            centerLatitude = 11.0,
            centerLongitude = 21.0,
            radiusMeters = 200.0,
            isActive = false
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Updated Geofence", updated.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PutGeofence_NotFound_Returns404()
    {
        var update = await _client.PutAsJsonAsync($"/api/geofences/{Guid.NewGuid()}", new
        {
            name = "Nonexistent",
            centerLatitude = 0.0,
            centerLongitude = 0.0,
            radiusMeters = 100.0,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task DeleteGeofence_Should_Return204()
    {
        var create = await _client.PostAsJsonAsync("/api/geofences", new
        {
            name = "To Delete Geofence",
            centerLatitude = 5.0,
            centerLongitude = 5.0,
            radiusMeters = 50.0,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = created.GetProperty("id").GetString();

        var del = await _client.DeleteAsync($"/api/geofences/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }
}
