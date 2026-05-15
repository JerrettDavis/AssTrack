using Reqnroll;

namespace AssTrack.E2ETests.Steps;

[Binding]
public class AssetDeviceSteps
{
    private readonly SharedStepContext _context;

    public AssetDeviceSteps(Reqnroll.ScenarioContext scenarioContext)
    {
        _context = new SharedStepContext(scenarioContext);
    }

    [Given(@"an asset named ""([^""]*)"" exists via the API")]
    public async Task GivenAnAssetNamedExistsViaTheAPI(string name)
    {
        var data = new Dictionary<string, object>
        {
            ["name"] = name,
            ["description"] = "E2E test asset",
            ["category"] = "Vehicle"
        };
        var assetId = await _context.ApiClient.CreateAssetAsync(data);
        _context.AssetId = assetId;
    }

    [Given(@"a device with identifier ""([^""]*)"" is linked to the asset")]
    public async Task GivenADeviceWithIdentifierIsLinkedToTheAsset(string identifier)
    {
        var actualIdentifier = $"{identifier}-{Guid.NewGuid():N}";
        var data = new Dictionary<string, object>
        {
            ["identifier"] = actualIdentifier,
            ["label"] = actualIdentifier,
            ["protocol"] = "https",
            ["assetId"] = _context.AssetId!
        };
        var deviceId = await _context.ApiClient.CreateDeviceAsync(data);
        _context.DeviceId = deviceId;
        _context.DeviceIdentifier = actualIdentifier;
    }

    [Given(@"an unassigned bridge device named ""([^""]*)"" exists via the API")]
    public async Task GivenAnUnassignedBridgeDeviceNamedExistsViaTheAPI(string name)
    {
        var actualIdentifier = $"meshtastic:{Guid.NewGuid():N}";
        var data = new Dictionary<string, object>
        {
            ["identifier"] = actualIdentifier,
            ["label"] = name,
            ["protocol"] = "meshtastic",
            ["provider"] = "meshtastic",
            ["externalId"] = actualIdentifier.Replace("meshtastic:", "!"),
            ["tags"] = "meshtastic, e2e"
        };
        var deviceId = await _context.ApiClient.CreateDeviceAsync(data);
        _context.UnassignedDeviceId = deviceId;
    }

    [Given(@"a due maintenance schedule named ""([^""]*)"" exists for the asset")]
    public async Task GivenADueMaintenanceScheduleNamedExistsForTheAsset(string title)
    {
        var assetId = _context.AssetId ?? throw new InvalidOperationException("AssetId not set");
        var scheduleId = await _context.ApiClient.CreateMaintenanceScheduleAsync(new Dictionary<string, object>
        {
            ["assetId"] = assetId,
            ["title"] = title,
            ["serviceType"] = "inspection",
            ["intervalOdometerKm"] = 1000,
            ["lastOdometerKm"] = 20000
        });
        _context.MaintenanceScheduleId = scheduleId;

        await _context.ApiClient.CreateSensorReadingAsync(new Dictionary<string, object>
        {
            ["assetId"] = assetId,
            ["sensorType"] = "odometer",
            ["name"] = "Odometer",
            ["numericValue"] = 21000,
            ["unit"] = "km",
            ["observedAt"] = DateTime.UtcNow.ToString("o")
        });
    }

    [When(@"I complete maintenance schedule ""([^""]*)""")]
    public async Task WhenICompleteMaintenanceSchedule(string title)
    {
        var scheduleId = _context.MaintenanceScheduleId ?? throw new InvalidOperationException("MaintenanceScheduleId not set");
        await _context.ApiClient.CompleteMaintenanceScheduleAsync(scheduleId, new Dictionary<string, object>
        {
            ["notes"] = "Completed in e2e"
        });
        await _context.Page.ReloadAsync();
        await _context.Page.GetByText("Recent service").First.WaitForAsync();
        await _context.Page.GetByText("Completed in e2e").First.WaitForAsync();
    }
}
