using Microsoft.Playwright;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.PageObjects;

public class AlertsPageObject
{
    private readonly IPage _page;

    public AlertsPageObject(IPage page) => _page = page;

    public async Task NavigateAsync() =>
        await _page.GotoAsync($"{E2ESettings.FrontendUrl}/alerts");

    public async Task<bool> HasTableCellWithTextAsync(string text) =>
        await _page.Locator($"td:has-text('{text}')").CountAsync() > 0;

    public async Task<bool> HasFilterTabAsync(string tabName) =>
        await _page.Locator($".alert-tabs button:has-text('{tabName}')").CountAsync() > 0;
}
