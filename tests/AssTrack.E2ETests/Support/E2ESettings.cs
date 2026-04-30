namespace AssTrack.E2ETests.Support;

public class E2ESettings
{
    public static int BackendPort => 5099;
    public static int FrontendPort => 5174;
    public static string BackendUrl => $"http://localhost:{BackendPort}";
    public static string FrontendUrl => $"http://localhost:{FrontendPort}";
    
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
    
    public static string GetTempDbPath()
    {
        var tempPath = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
        return Path.Combine(tempPath, $"asstrack_e2e_{Guid.NewGuid()}.db");
    }
}
