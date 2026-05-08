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
        // On Linux CI, install OS-level dependencies for Chromium
        var installArgs = OperatingSystem.IsLinux()
            ? new[] { "install", "--with-deps", "chromium" }
            : new[] { "install", "chromium" };
        Microsoft.Playwright.Program.Main(installArgs);
        
        if (E2ESettings.UseExternalApp)
        {
            await WaitForExternalAppAsync();
            await CleanupE2EDataAsync();
        }
        else
        {
            _dbPath = E2ESettings.GetTempDbPath();

            _backend = new BackendProcess(_dbPath);
            await _backend.StartAsync();

            _frontend = new FrontendProcess();
            await _frontend.StartAsync();
        }
        
        _playwrightFixture = new PlaywrightFixture();
        await _playwrightFixture.InitializeAsync();
    }

    private static async Task WaitForExternalAppAsync()
    {
        using var client = new HttpClient();
        for (var i = 0; i < 60; i++)
        {
            try
            {
                var backend = await client.GetAsync($"{E2ESettings.BackendUrl}/healthz/live");
                var frontend = await client.GetAsync(E2ESettings.FrontendUrl);
                if (backend.IsSuccessStatusCode && frontend.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Retry while the externally orchestrated app finishes starting.
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("External E2E app failed to become ready within expected time");
    }

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        if (E2ESettings.UseExternalApp)
        {
            await CleanupE2EDataAsync();
        }

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
        if (scenarioContext.TryGetValue("ApiClient", out ApiClient apiClient))
        {
            await CleanupE2EDataAsync(apiClient);
        }

        if (scenarioContext.TryGetValue("Page", out IPage page))
        {
            await page.CloseAsync();
        }
    }

    private static async Task CleanupE2EDataAsync(ApiClient? apiClient = null)
    {
        try
        {
            await (apiClient ?? new ApiClient()).CleanupE2EDataAsync();
        }
        catch
        {
            // Best effort cleanup; test assertions should surface functional failures separately.
        }
    }
}
