using Microsoft.Playwright;

namespace AssTrack.E2ETests.Support;

public class PlaywrightFixture : IDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task<IPage> CreatePageAsync()
    {
        if (_browser == null)
            throw new InvalidOperationException("Browser not initialized");
        
        var context = await _browser.NewContextAsync();
        return await context.NewPageAsync();
    }

    public void Dispose()
    {
        _browser?.CloseAsync().Wait();
        _playwright?.Dispose();
    }
}
