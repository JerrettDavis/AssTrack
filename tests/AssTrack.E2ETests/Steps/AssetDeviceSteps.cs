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
    }
}
