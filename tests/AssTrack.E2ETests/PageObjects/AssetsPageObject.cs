using Microsoft.Playwright;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.PageObjects;

public class AssetsPageObject
{
    private readonly IPage _page;

    public AssetsPageObject(IPage page) => _page = page;

    public async Task NavigateAsync() =>
        await _page.GotoAsync($"{E2ESettings.FrontendUrl}/");

    public async Task<bool> ContainsTextAsync(string text) =>
        await _page.GetByText(text).CountAsync() > 0;
}
