using AssTrack.E2ETests.PageObjects;
using AssTrack.E2ETests.Support;
using FluentAssertions;
using Microsoft.Playwright;
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
        if (!string.IsNullOrWhiteSpace(_context.DeviceId))
        {
            await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/map?device={Uri.EscapeDataString(_context.DeviceId)}", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            return;
        }

        var page = new MapPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the alerts page")]
    public async Task WhenINavigateToTheAlertsPage()
    {
        var page = new AlertsPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the unscoped map page")]
    public async Task WhenINavigateToTheUnscopedMapPage()
    {
        var page = new MapPageObject(_context.Page);
        await page.NavigateAsync();
    }

    [When(@"I navigate to the reports page")]
    public async Task WhenINavigateToTheReportsPage()
    {
        await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/reports");
        await _context.Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Reports" }).WaitForAsync();
    }

    [When(@"I navigate to the audit page")]
    public async Task WhenINavigateToTheAuditPage()
    {
        await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/audit");
        await _context.Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Audit" }).WaitForAsync();
    }

    [When(@"I navigate to the signals page")]
    public async Task WhenINavigateToTheSignalsPage()
    {
        await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/signals");
        await _context.Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Signals" }).WaitForAsync();
    }

    [When(@"I publish an enterprise signal named ""([^""]*)""")]
    public async Task WhenIPublishAnEnterpriseSignalNamed(string signalName)
    {
        var form = _context.Page.GetByTestId("signal-publish-form");
        await form.Locator("label.field", new() { HasTextString = "Source" }).Locator("input").FillAsync("e2e-console");
        await form.Locator("label.field", new() { HasTextString = "Event type" }).Locator("input").FillAsync("e2e.signal");
        await form.Locator("label.field", new() { HasTextString = "Subject name" }).Locator("input").FillAsync(signalName);
        await form.Locator("label.field", new() { HasTextString = "Message" }).Locator("input").FillAsync($"Enterprise signal {signalName}");
        await form.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Publish Signal" }).ClickAsync();
        await _context.Page.GetByText("Published e2e.signal").WaitForAsync();
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

    [When(@"I navigate to the webhooks page")]
    public async Task WhenINavigateToTheWebhooksPage()
    {
        await _context.Page.GotoAsync($"{E2ESettings.FrontendUrl}/webhooks");
        await _context.Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Webhooks" }).WaitForAsync();
    }

    [When(@"I create a webhook subscription named ""([^""]*)""")]
    public async Task WhenICreateAWebhookSubscriptionNamed(string subscriptionName)
    {
        var section = _context.Page.Locator("xpath=//div[contains(@class,'inline-form')][.//h2[normalize-space()='Webhook Subscriptions']]");
        await section.Locator("label.field", new() { HasTextString = "Name" }).Locator("input").FillAsync(subscriptionName);
        await section.Locator("label.field", new() { HasTextString = "Event types" }).Locator("input").FillAsync("enterprise_signal");
        await section.Locator("label.field", new() { HasTextString = "Target URL" }).Locator("input").FillAsync($"https://hooks.example.com/e2e/{Guid.NewGuid():N}");
        await section.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add Subscription" }).ClickAsync();
        await _context.Page.GetByText(subscriptionName).WaitForAsync();
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
        if (!string.IsNullOrWhiteSpace(_context.DeviceId))
        {
            var deviceSelect = _context.Page.Locator("xpath=//div[contains(@class,'map-panel')][.//h2[normalize-space()='Trail']]//select").First;
            try
            {
                await deviceSelect.WaitForAsync(new() { Timeout = 10000 });
                await deviceSelect.SelectOptionAsync(_context.DeviceId);

                var panel = MapDetailPanel();
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (DateTime.UtcNow < deadline)
                {
                    var text = await panel.InnerTextAsync(new() { Timeout = 1000 });
                    if (text.Contains("Observation log", StringComparison.OrdinalIgnoreCase)
                        && (_context.DeviceIdentifier is null || text.Contains(_context.DeviceIdentifier, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    await _context.Page.WaitForTimeoutAsync(250);
                }

                return;
            }
            catch (Exception)
            {
                // Fall through to marker selection when a scenario intentionally has no scoped node ready yet.
            }
        }

        var marker = _context.Page.Locator(".device-marker").First;
        var markers = _context.Page.Locator(".device-marker");
        var clusters = _context.Page.Locator(".device-cluster-marker");

        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (await TryClickVisibleAsync(markers))
            {
                return;
            }

            if (await TryClickVisibleAsync(clusters))
            {
                await _context.Page.WaitForTimeoutAsync(500);
                continue;
            }

            await _context.Page.WaitForTimeoutAsync(500);
        }

        await marker.WaitForAsync();
        await marker.ClickAsync(new() { Force = true });
    }

    private static async Task<bool> TryClickVisibleAsync(ILocator locator)
    {
        var count = await locator.CountAsync();
        for (var index = 0; index < count; index++)
        {
            var item = locator.Nth(index);
            if (!await item.IsVisibleAsync()) continue;

            var box = await item.BoundingBoxAsync();
            if (box is null || box.Width <= 0 || box.Height <= 0 || box.X + box.Width < 0 || box.Y + box.Height < 0)
            {
                continue;
            }

            try
            {
                await item.ClickAsync(new() { Force = true, Timeout = 1000 });
                return true;
            }
            catch (PlaywrightException)
            {
                continue;
            }
        }

        return false;
    }

    [Then(@"the map node details panel is available")]
    public async Task ThenTheMapNodeDetailsPanelIsAvailable()
    {
        var panel = MapDetailPanel();
        await panel.WaitForAsync();
        await ExpectAnyPanelTextAsync(panel, "Asset", "Tracker");
        await panel.GetByText("Observation log").WaitForAsync();
    }

    private ILocator MapDetailPanel()
    {
        return _context.Page.Locator("[data-testid='map-node-detail-panel'], .map-detail-panel[aria-label='Selected node details']").First;
    }

    [Then(@"the map trail uses luminosity decay")]
    public async Task ThenTheMapTrailUsesLuminosityDecay()
    {
        try
        {
            await _context.Page.GetByRole(AriaRole.Button, new() { Name = "Follow" }).ClickAsync(new() { Timeout = 1000 });
        }
        catch (Exception)
        {
            // Follow is an enhancement for scoped maps; trail rendering is still asserted below.
        }

        var segments = _context.Page.Locator("path.map-trail-segment, path[stroke^='hsl(']");

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && await segments.CountAsync() < 2)
        {
            await _context.Page.WaitForTimeoutAsync(250);
        }

        var count = await segments.CountAsync();
        if (count < 2)
        {
            var allSvgPaths = await _context.Page.Locator("svg path").CountAsync();
            var trailPanelText = await _context.Page.Locator("xpath=//div[contains(@class,'map-panel')][.//h2[normalize-space()='Trail']]").First.InnerTextAsync();
            count.Should().BeGreaterThanOrEqualTo(2, $"expected rendered decayed trail segments; svg path count was {allSvgPaths}, trail panel was: {trailPanelText}");
        }

        var strokes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < count; index++)
        {
            var segment = segments.Nth(index);
            var stroke = await segment.GetAttributeAsync("stroke")
                ?? await segment.EvaluateAsync<string?>("el => getComputedStyle(el).stroke");
            stroke.Should().NotBeNullOrWhiteSpace();
            strokes.Add(stroke!);

            var opacity = await segment.GetAttributeAsync("stroke-opacity");
            if (!string.IsNullOrWhiteSpace(opacity))
            {
                opacity.Should().Be("1");
            }
        }

        strokes.Count.Should().BeGreaterThan(1, "trail decay should vary stroke luminosity instead of opacity");
    }

    [Then(@"all-map timeline trails are suppressed")]
    public async Task ThenAllMapTimelineTrailsAreSuppressed()
    {
        await _context.Page.GetByRole(AriaRole.Button, new() { Name = "Play" }).ClickAsync();
        await _context.Page.WaitForTimeoutAsync(500);

        var decayedTrailSegments = _context.Page.Locator("path[stroke^='hsl(']");
        (await decayedTrailSegments.CountAsync()).Should().Be(0, "all-map playback should avoid unreadable overlapping tracker history");
    }

    private static async Task ExpectAnyPanelTextAsync(ILocator panel, params string[] values)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        do
        {
            var text = await panel.InnerTextAsync();
            if (values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase))) return;
            await Task.Delay(250);
        }
        while (DateTime.UtcNow < deadline);

        false.Should().BeTrue($"Expected panel to contain one of: {string.Join(", ", values)}");
    }

    [Then(@"the page contains ""([^""]*)""")]
    public async Task ThenThePageContains(string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string bodyText;
        do
        {
            bodyText = await _context.Page.Locator("body").InnerTextAsync();
            if (bodyText.Contains(text, StringComparison.OrdinalIgnoreCase)) return;
            await _context.Page.WaitForTimeoutAsync(250);
        }
        while (DateTime.UtcNow < deadline);

        false.Should().BeTrue($"Expected page to contain text '{text}'. Body text: {bodyText}");
    }
}
