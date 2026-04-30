using Microsoft.Playwright;
using Reqnroll;
using AssTrack.E2ETests.Support;

namespace AssTrack.E2ETests.Hooks;

[Binding]
public class LifecycleHooks
{
    private static BackendProcess? _backend;
    private static FrontendProcess? _frontend;
    private static PlaywrightFixture? _playwrightFixture;
    private static string? _dbPath;

    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        
        _dbPath = E2ESettings.GetTempDbPath();
        
        _backend = new BackendProcess(_dbPath);
        await _backend.StartAsync();
        
        _frontend = new FrontendProcess();
        await _frontend.StartAsync();
        
        _playwrightFixture = new PlaywrightFixture();
        await _playwrightFixture.InitializeAsync();
    }

    [AfterTestRun]
    public static void AfterTestRun()
    {
        _frontend?.Dispose();
        _backend?.Dispose();
        _playwrightFixture?.Dispose();
        
        if (_dbPath != null && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [BeforeScenario]
    public async Task BeforeScenario(ScenarioContext scenarioContext)
    {
        if (_playwrightFixture == null)
            throw new InvalidOperationException("Playwright not initialized");
            
        var page = await _playwrightFixture.CreatePageAsync();
        scenarioContext["Page"] = page;
        scenarioContext["ApiClient"] = new ApiClient();
    }

    [AfterScenario]
    public async Task AfterScenario(ScenarioContext scenarioContext)
    {
        if (scenarioContext.TryGetValue("Page", out IPage page))
        {
            await page.CloseAsync();
        }
    }
}
