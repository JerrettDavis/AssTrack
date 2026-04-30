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
}
