using Microsoft.Playwright;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.PageObjects;

public class AlertsPageObject
{
    private readonly IPage _page;

    public AlertsPageObject(IPage page) => _page = page;

    public async Task NavigateAsync() =>
        await _page.GotoAsync($"{E2ESettings.FrontendUrl}/alerts", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

    public async Task<bool> HasTableCellWithTextAsync(string text)
    {
        try
        {
            await _page.Locator($"td:has-text('{text}')").First.WaitForAsync();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<bool> HasFilterTabAsync(string tabName)
    {
        try
        {
            await _page.Locator($".alert-tabs button:has-text('{tabName}')").First.WaitForAsync();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
