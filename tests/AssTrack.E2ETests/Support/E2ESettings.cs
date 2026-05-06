namespace AssTrack.E2ETests.Support;

public class E2ESettings
{
    public static int BackendPort =>
        int.TryParse(Environment.GetEnvironmentVariable("E2E_BACKEND_PORT"), out var p) ? p : 5099;

    public static int FrontendPort =>
        int.TryParse(Environment.GetEnvironmentVariable("E2E_FRONTEND_PORT"), out var p) ? p : 5174;

    public static string BackendUrl => $"http://127.0.0.1:{BackendPort}";
    public static string FrontendUrl => $"http://127.0.0.1:{FrontendPort}";
    public static string ApiKey => Environment.GetEnvironmentVariable("E2E_API_KEY") ?? "e2e-test-key";
    public static bool UseExternalApp =>
        string.Equals(Environment.GetEnvironmentVariable("E2E_STARTUP_MODE"), "External", StringComparison.OrdinalIgnoreCase);

    public static string GetRepoRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var dirInfo = new DirectoryInfo(baseDir);
        while (dirInfo != null && !File.Exists(Path.Combine(dirInfo.FullName, "AssTrack.sln")))
        {
            dirInfo = dirInfo.Parent;
        }
        if (dirInfo == null)
            throw new InvalidOperationException("Could not find repository root");
        return dirInfo.FullName;
    }

    public static string GetTempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"asstrack_e2e_{Guid.NewGuid()}.db");
}
