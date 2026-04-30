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
}
