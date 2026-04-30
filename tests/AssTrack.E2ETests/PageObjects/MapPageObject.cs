using Microsoft.Playwright;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.PageObjects;

public class MapPageObject
{
    private readonly IPage _page;

    public MapPageObject(IPage page) => _page = page;

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{E2ESettings.FrontendUrl}/map");
        await Task.Delay(1000);
        await _page.ReloadAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> ContainsTextAsync(string text) =>
        await _page.GetByText(text).CountAsync() > 0;
}
