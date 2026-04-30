using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AssTrack.Tests.Api;

public class DeviceApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    [Fact]
    public async Task PostDevice_ThenGetDevices_ContainsNewDevice()
    {
        var payload = new { Identifier = "DEV-001", Label = "Test Device", Protocol = "MQTT" };
        var post = await _client.PostAsJsonAsync("/api/devices", payload);
        post.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/devices");
        Assert.NotNull(list);
        Assert.Contains(list, d => d.GetProperty("identifier").GetString() == "DEV-001");
    }

    [Fact]
    public async Task PostDevice_DuplicateIdentifier_Returns409()
    {
        var payload = new { Identifier = "DEV-DUP", Label = "Dup", Protocol = "MQTT" };
        var first = await _client.PostAsJsonAsync("/api/devices", payload);
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/devices", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PutDevice_Should_UpdateDevice()
    {
        var create = await _client.PostAsJsonAsync("/api/devices", new { Identifier = "DEV-UPD-001", Label = "Original", Protocol = "MQTT" });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var update = await _client.PutAsJsonAsync($"/api/devices/{id}", new { Identifier = "DEV-UPD-001", Label = "Updated", Protocol = "https" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated", updated.GetProperty("label").GetString());
    }

    [Fact]
    public async Task PutDevice_NotFound_Returns404()
    {
        var update = await _client.PutAsJsonAsync($"/api/devices/{Guid.NewGuid()}", new { Identifier = "DEV-NOTFOUND", Label = "X", Protocol = "https" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task DeleteDevice_Should_Return204_AndBeGone()
    {
        var create = await _client.PostAsJsonAsync("/api/devices", new { Identifier = "DEV-DEL-002", Label = "ToDelete", Protocol = "MQTT" });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var del = await _client.DeleteAsync($"/api/devices/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await _client.GetAsync($"/api/devices/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task GetDeviceById_Should_ReturnDevice()
    {
        var create = await _client.PostAsJsonAsync("/api/devices", new { Identifier = "DEV-GET-003", Label = "GetMe", Protocol = "MQTT" });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var get = await _client.GetFromJsonAsync<JsonElement>($"/api/devices/{id}");
        Assert.Equal("DEV-GET-003", get.GetProperty("identifier").GetString());
    }
}
