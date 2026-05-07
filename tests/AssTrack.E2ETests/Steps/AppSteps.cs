using AssTrack.E2ETests.PageObjects;
using AssTrack.E2ETests.Support;
using FluentAssertions;
using Reqnroll;

namespace AssTrack.E2ETests.Steps;

[Binding]
public class AppSteps
{
    private readonly SharedStepContext _context;

    public AppSteps(Reqnroll.ScenarioContext scenarioContext)
    {
        _context = new SharedStepContext(scenarioContext);
    }

    [When(@"I navigate to the assets page")]
    public async Task WhenINavigateToTheAssetsPage()
    {
        var page = new AssetsPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the devices page")]
    public async Task WhenINavigateToTheDevicesPage()
    {
        var page = new DevicesPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the map page")]
    public async Task WhenINavigateToTheMapPage()
    {
        var page = new MapPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the alerts page")]
    public async Task WhenINavigateToTheAlertsPage()
    {
        var page = new AlertsPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the geofences page")]
    public async Task WhenINavigateToTheGeofencesPage()
    {
        await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/geofences");
        await _context.Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Geofences" }).WaitForAsync();
    }

    [When(@"I navigate to the bridge page")]
    public async Task WhenINavigateToTheBridgePage()
    {
        await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/integrations");
        await _context.Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Bridge Gateway" }).WaitForAsync();
    }

    [When(@"I configure bridge provider ""([^""]*)""")]
    public async Task WhenIConfigureBridgeProvider(string providerName)
    {
        var configureButton = _context.Page.Locator($"xpath=//article[.//h3[normalize-space()='{providerName}']]//button[normalize-space()='Configure']");
        await configureButton.ScrollIntoViewIfNeededAsync();
        await configureButton.ClickAsync();
        await _context.Page.GetByTestId("bridge-feed-form").WaitForAsync();
    }

    [Then(@"the bridge feed form is focused with bridge key ""([^""]*)""")]
    public async Task ThenTheBridgeFeedFormIsFocusedWithBridgeKey(string expectedBridgeKey)
    {
        var form = _context.Page.GetByTestId("bridge-feed-form");
        await form.ScrollIntoViewIfNeededAsync();
        await form.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });

        var bridgeKey = form.Locator("input[name='bridgeKey']");
        await bridgeKey.WaitForAsync();
        var value = await bridgeKey.InputValueAsync();
        value.Should().Be(expectedBridgeKey);

        var activeName = await _context.Page.EvaluateAsync<string?>("() => document.activeElement?.getAttribute('name')");
        activeName.Should().Be("bridgeKey");
    }

    [Then(@"the bridge checkbox ""([^""]*)"" is compact")]
    public async Task ThenTheBridgeCheckboxIsCompact(string label)
    {
        var checkbox = _context.Page.Locator($"xpath=//label[contains(@class,'check-field')][.//span[normalize-space()='{label}']]//input[@type='checkbox']").First;
        await checkbox.WaitForAsync();
        var box = await checkbox.BoundingBoxAsync();
        box.Should().NotBeNull();
        box!.Width.Should().BeLessThan(28);
        box.Height.Should().BeLessThan(28);
    }

    [Then(@"the Meshtastic public MQTT defaults are configured")]
    public async Task ThenTheMeshtasticPublicMqttDefaultsAreConfigured()
    {
        var form = _context.Page.GetByTestId("bridge-feed-form");
        (await form.GetByLabel("MQTT host").InputValueAsync()).Should().Be("mqtt.meshtastic.org");
        (await form.GetByLabel("MQTT port").InputValueAsync()).Should().Be("1883");
        (await form.GetByLabel("Topic").InputValueAsync()).Should().Be("msh/US/2/json/LongFast/#");
        (await form.GetByLabel("Username").InputValueAsync()).Should().Be("meshdev");
        (await form.GetByLabel("Password").InputValueAsync()).Should().Be("large4cats");
        await _context.Page.GetByText("For a private channel on the public server").WaitForAsync();
    }

    [Then(@"the map layer controls are available")]
    public async Task ThenTheMapLayerControlsAreAvailable()
    {
        var panel = _context.Page.GetByTestId("map-layers-panel");
        await panel.WaitForAsync();
        await panel.GetByLabel("Base map").SelectOptionAsync("satellite");
        await panel.GetByLabel("Provider").WaitForAsync();
        await panel.GetByLabel("Bridge feed").WaitForAsync();
        await panel.GetByText("Devices").WaitForAsync();
        await panel.GetByText("Trail").WaitForAsync();
        await panel.GetByText("Geofences").WaitForAsync();
    }

    [When(@"I select the first map node")]
    public async Task WhenISelectTheFirstMapNode()
    {
        var marker = _context.Page.Locator(".device-marker").First;
        var cluster = _context.Page.Locator(".device-cluster-marker").First;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (await marker.CountAsync() > 0 && await marker.IsVisibleAsync())
            {
                await marker.ClickAsync(new() { Force = true });
                return;
            }

            if (await cluster.CountAsync() > 0 && await cluster.IsVisibleAsync())
            {
                await cluster.ClickAsync(new() { Force = true });
                await _context.Page.WaitForTimeoutAsync(500);
                continue;
            }

            await _context.Page.WaitForTimeoutAsync(500);
        }

        await marker.WaitForAsync();
        await marker.ClickAsync(new() { Force = true });
    }

    [Then(@"the map node details panel is available")]
    public async Task ThenTheMapNodeDetailsPanelIsAvailable()
    {
        var panel = _context.Page.GetByTestId("map-node-detail-panel");
        await panel.WaitForAsync();
        await panel.GetByText("Tracker node").WaitForAsync();
        await panel.GetByText("Observation log").WaitForAsync();
    }

    [Then(@"the page contains ""([^""]*)""")]
    public async Task ThenThePageContains(string text)
    {
        try
        {
            await _context.Page.GetByText(text).First.WaitForAsync();
        }
        catch (TimeoutException)
        {
            false.Should().BeTrue($"Expected page to contain text '{text}'");
        }
    }
}
