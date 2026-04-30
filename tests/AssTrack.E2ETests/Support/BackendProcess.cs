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
                var response = await client.GetAsync($"{E2ESettings.BackendUrl}/api/assets");
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
            var killProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {processId} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            killProcess.Start();
            killProcess.WaitForExit();
        }
        catch
        {
            // Best effort
        }
    }

    private static string ResolveDotnetCommand()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet")
        };

        foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var dotnetPath = Path.Combine(candidate!, "dotnet.exe");
            if (File.Exists(dotnetPath))
            {
                return dotnetPath;
            }
        }

        return "dotnet";
    }
}
