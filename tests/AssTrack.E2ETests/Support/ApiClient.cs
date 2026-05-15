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

    public async Task<string> CompleteMaintenanceScheduleAsync(string scheduleId, Dictionary<string, object> data)
    {
        var response = await _client.PostAsJsonAsync($"/api/maintenance/schedules/{scheduleId}/complete", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No maintenance service record ID returned");
    }

    public async Task<string> CreateAlertRouteAsync(Dictionary<string, object?> data)
    {
        var response = await _client.PostAsJsonAsync("/api/alert-routes", data);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new Exception("No alert route ID returned");
    }

    public async Task CleanupE2EDataAsync()
    {
        await CleanupWebhookSubscriptionsAsync();
        var response = await _client.DeleteAsync("/api/system/maintenance/e2e-data");
        response.EnsureSuccessStatusCode();
    }

    private async Task CleanupWebhookSubscriptionsAsync()
    {
        try
        {
            var response = await _client.GetAsync("/api/webhooks/subscriptions");
            if (!response.IsSuccessStatusCode) return;

            var subscriptions = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var subscription in subscriptions.EnumerateArray())
            {
                var name = subscription.GetProperty("name").GetString();
                if (name?.StartsWith("E2E ", StringComparison.OrdinalIgnoreCase) != true) continue;

                var id = subscription.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    await _client.DeleteAsync($"/api/webhooks/subscriptions/{id}");
                }
            }
        }
        catch
        {
            // Best effort cleanup; scenario assertions should surface functional failures separately.
        }
    }
}
