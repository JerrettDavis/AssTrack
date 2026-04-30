using System.Diagnostics;

namespace AssTrack.E2ETests.Support;

public class BackendProcess : IDisposable
{
    private Process? _process;
    private readonly string _dbPath;

    public BackendProcess(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task StartAsync()
    {
        var repoRoot = E2ESettings.GetRepoRoot();
        var apiProjectPath = Path.Combine(repoRoot, "src", "AssTrack.Api");
        var apiCsprojPath = Path.Combine(apiProjectPath, "AssTrack.Api.csproj");

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ResolveDotnetCommand(),
                Arguments = $"run --project \"{apiCsprojPath}\" --no-build --no-restore --no-launch-profile",
                WorkingDirectory = apiProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _process.StartInfo.Environment["ASPNETCORE_URLS"] = E2ESettings.BackendUrl;
        _process.StartInfo.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={_dbPath}";
        _process.StartInfo.Environment["ConnectionStrings__AssTrack"] = $"Data Source={_dbPath}";
        _process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        _process.StartInfo.Environment["Auth__ApiKey"] = E2ESettings.ApiKey;

        _process.Start();

        await WaitForStartupAsync();
    }

    private async Task WaitForStartupAsync()
    {
        using var client = new HttpClient();
        var maxAttempts = 60;
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var response = await client.GetAsync($"{E2ESettings.BackendUrl}/healthz/live");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Ignore and retry
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException("Backend failed to start within expected time");
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            KillProcessTree(_process.Id);
            _process.Dispose();
        }
    }

    private static void KillProcessTree(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            proc.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort
        }
    }

    private static string ResolveDotnetCommand()
    {
        // Check DOTNET_ROOT env var first (set by setup-dotnet in CI)
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            var exe = Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(exe)) return exe;
        }

        // Windows: check standard install locations
        if (OperatingSystem.IsWindows())
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe")
            };
            foreach (var c in candidates.Where(File.Exists))
                return c;
        }

        return "dotnet";
    }
}
