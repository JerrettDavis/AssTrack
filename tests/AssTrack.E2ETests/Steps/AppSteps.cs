using AssTrack.E2ETests.PageObjects;
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
        await _context.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
    }

    [When(@"I navigate to the devices page")]
    public async Task WhenINavigateToTheDevicesPage()
    {
        var page = new DevicesPageObject(_context.Page);
        await page.NavigateAsync();
        await _context.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
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
        await _context.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
    }

    [Then(@"the page contains ""([^""]*)""")]
    public async Task ThenThePageContains(string text)
    {
        var hasText = await _context.Page.GetByText(text).CountAsync() > 0;
        hasText.Should().BeTrue($"Expected page to contain text '{text}'");
    }
}
