using System.Net.Http.Json;
using System.Text.Json;

namespace AssTrack.E2ETests.Support;

public class ApiClient
{
    private readonly HttpClient _client;

    public ApiClient()
    {
        _client = new HttpClient { BaseAddress = new Uri(E2ESettings.BackendUrl) };
        _client.DefaultRequestHeaders.Add("X-Api-Key", E2ESettings.ApiKey);
    }

    public async Task<string> CreateAssetAsync(Dictionary<string, object> data)
    {
        var response = await _client.PostAsJsonAsync("/api/assets", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No asset ID returned");
    }

    public async Task<string> CreateDeviceAsync(Dictionary<string, object> data)
    {
        var response = await _client.PostAsJsonAsync("/api/devices", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No device ID returned");
    }

    public async Task<string> CreateObservationAsync(Dictionary<string, object> data)
    {
        var response = await _client.PostAsJsonAsync("/api/observations", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No observation ID returned");
    }

    public async Task<string> CreateSensorReadingAsync(Dictionary<string, object> data)
    {
        var response = await _client.PostAsJsonAsync("/api/sensors/readings", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No sensor reading ID returned");
    }

    public async Task<string> CreateMaintenanceScheduleAsync(Dictionary<string, object> data)
    {
        var response = await _client.PostAsJsonAsync("/api/maintenance/schedules", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No maintenance schedule ID returned");
    }
}
