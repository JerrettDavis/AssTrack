using Microsoft.Playwright;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.Steps;

public class SharedStepContext
{
    private readonly Reqnroll.ScenarioContext _scenarioContext;

    public SharedStepContext(Reqnroll.ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    public IPage Page => (IPage)_scenarioContext["Page"];

    public ApiClient ApiClient => (ApiClient)_scenarioContext["ApiClient"];

    public string? AssetId
    {
        get => _scenarioContext.TryGetValue("AssetId", out string? id) ? id : null;
        set => _scenarioContext["AssetId"] = value;
    }

    public string? DeviceId
    {
        get => _scenarioContext.TryGetValue("DeviceId", out string? id) ? id : null;
        set => _scenarioContext["DeviceId"] = value;
    }

    public string? DeviceIdentifier
    {
        get => _scenarioContext.TryGetValue("DeviceIdentifier", out string? identifier) ? identifier : null;
        set => _scenarioContext["DeviceIdentifier"] = value;
    }

    public string? UnassignedDeviceId
    {
        get => _scenarioContext.TryGetValue("UnassignedDeviceId", out string? id) ? id : null;
        set => _scenarioContext["UnassignedDeviceId"] = value;
    }

    public string? MaintenanceScheduleId
    {
        get => _scenarioContext.TryGetValue("MaintenanceScheduleId", out string? id) ? id : null;
        set => _scenarioContext["MaintenanceScheduleId"] = value;
    }
}
