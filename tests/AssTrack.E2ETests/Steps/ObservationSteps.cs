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
}
