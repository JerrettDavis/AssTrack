using AssTrack.E2ETests.PageObjects;
using FluentAssertions;
using Reqnroll;

namespace AssTrack.E2ETests.Steps;

[Binding]
public class AlertSteps
{
    private readonly SharedStepContext _context;

    public AlertSteps(Reqnroll.ScenarioContext scenarioContext)
    {
        _context = new SharedStepContext(scenarioContext);
    }

    [Then(@"the alerts table contains a cell with ""([^""]*)""")]
    public async Task ThenTheAlertsTableContainsACellWith(string value)
    {
        var page = new AlertsPageObject(_context.Page);
        var hasValue = await page.HasTableCellWithTextAsync(value);
        hasValue.Should().BeTrue($"Expected alerts table to contain cell with value '{value}'");
    }

    [Then(@"the alerts page has filter tab ""([^""]*)""")]
    public async Task ThenTheAlertsPageHasFilterTab(string tabName)
    {
        var page = new AlertsPageObject(_context.Page);
        var hasTab = await page.HasFilterTabAsync(tabName);
        hasTab.Should().BeTrue($"Expected alerts page to have filter tab '{tabName}'");
    }

    [When(@"I expand alert routing")]
    public async Task WhenIExpandAlertRouting()
    {
        await _context.Page.GetByText("Alert routing").ClickAsync();
    }

    [Given(@"an asset-filtered speed alert route named ""([^""]*)"" exists for the asset")]
    public async Task GivenAnAssetFilteredSpeedAlertRouteNamedExistsForTheAsset(string name)
    {
        var assetId = _context.AssetId ?? throw new InvalidOperationException("AssetId not set");
        await _context.ApiClient.CreateAlertRouteAsync(new Dictionary<string, object?>
        {
            ["name"] = name,
            ["isEnabled"] = true,
            ["eventType"] = "speed_alert",
            ["channel"] = "direct",
            ["provider"] = "meshtastic",
            ["assetId"] = assetId,
            ["integrationFeedId"] = null,
            ["externalPeerId"] = $"!e2e-{Guid.NewGuid():N}",
            ["displayName"] = name
        });
    }
}
