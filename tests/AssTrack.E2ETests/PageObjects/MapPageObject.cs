using Microsoft.Playwright;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.PageObjects;

public class MapPageObject
{
    private readonly IPage _page;

    public MapPageObject(IPage page) => _page = page;

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{E2ESettings.FrontendUrl}/map", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    public async Task<bool> ContainsTextAsync(string text)
    {
        try
        {
            await _page.GetByText(text).First.WaitForAsync();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
