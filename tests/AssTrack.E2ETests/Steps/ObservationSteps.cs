using Reqnroll;

namespace AssTrack.E2ETests.Steps;

[Binding]
public class ObservationSteps
{
    private readonly SharedStepContext _context;

    public ObservationSteps(Reqnroll.ScenarioContext scenarioContext)
    {
        _context = new SharedStepContext(scenarioContext);
    }

    [When(@"I post an observation for the device via the API")]
    public async Task WhenIPostAnObservationForTheDeviceViaTheAPI()
    {
        var data = new Dictionary<string, object>
        {
            ["deviceId"] = _context.DeviceId ?? throw new InvalidOperationException("DeviceId not set"),
            ["observedAt"] = DateTime.UtcNow.ToString("o"),
            ["latitude"] = 40.7128,
            ["longitude"] = -74.0060
        };
        await _context.ApiClient.CreateObservationAsync(data);
    }

    [When(@"I post an observation with speed (.*) for the device via the API")]
    public async Task WhenIPostAnObservationWithSpeedForTheDeviceViaTheAPI(double speed)
    {
        var data = new Dictionary<string, object>
        {
            ["deviceId"] = _context.DeviceId ?? throw new InvalidOperationException("DeviceId not set"),
            ["observedAt"] = DateTime.UtcNow.ToString("o"),
            ["latitude"] = 40.7128,
            ["longitude"] = -74.0060,
            ["speedKmh"] = speed
        };
        await _context.ApiClient.CreateObservationAsync(data);
    }

    [When(@"I post a three point trail for the device via the API")]
    public async Task WhenIPostAThreePointTrailForTheDeviceViaTheAPI()
    {
        var deviceId = _context.DeviceId ?? throw new InvalidOperationException("DeviceId not set");
        var start = DateTime.UtcNow.AddMinutes(-3);
        var points = new[]
        {
            new { ObservedAt = start, Latitude = 40.7120, Longitude = -74.0068 },
            new { ObservedAt = start.AddMinutes(1), Latitude = 40.7125, Longitude = -74.0063 },
            new { ObservedAt = start.AddMinutes(2), Latitude = 40.7130, Longitude = -74.0058 }
        };

        foreach (var point in points)
        {
            await _context.ApiClient.CreateObservationAsync(new Dictionary<string, object>
            {
                ["deviceId"] = deviceId,
                ["observedAt"] = point.ObservedAt.ToString("o"),
                ["latitude"] = point.Latitude,
                ["longitude"] = point.Longitude
            });
        }
    }


    [When(@"I post an observation for the unassigned bridge device via the API")]
    public async Task WhenIPostAnObservationForTheUnassignedBridgeDeviceViaTheAPI()
    {
        var data = new Dictionary<string, object>
        {
            ["deviceId"] = _context.UnassignedDeviceId ?? throw new InvalidOperationException("UnassignedDeviceId not set"),
            ["observedAt"] = DateTime.UtcNow.ToString("o"),
            ["latitude"] = 36.0594826,
            ["longitude"] = -95.8973805
        };
        await _context.ApiClient.CreateObservationAsync(data);
    }
}
