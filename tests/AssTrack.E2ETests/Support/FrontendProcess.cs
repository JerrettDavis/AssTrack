using System.Diagnostics;

namespace AssTrack.E2ETests.Support;

public class FrontendProcess : IDisposable
{
    private Process? _process;

    public async Task StartAsync()
    {
        var repoRoot = E2ESettings.GetRepoRoot();
        var frontendPath = Path.Combine(repoRoot, "frontend");
        var vitePath = Path.Combine(frontendPath, "node_modules", "vite", "bin", "vite.js");

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ResolveNodeCommand(),
                Arguments = $@"""{vitePath}"" --port {E2ESettings.FrontendPort} --host 127.0.0.1",
                WorkingDirectory = frontendPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        _process.StartInfo.Environment["VITE_E2E_PROXY_TARGET"] = E2ESettings.BackendUrl;
        _process.StartInfo.Environment["VITE_API_KEY"] = E2ESettings.ApiKey;

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
                var response = await client.GetAsync(E2ESettings.FrontendUrl);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Ignore and retry
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException("Frontend failed to start within expected time");
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

    private static string ResolveNodeCommand()
    {
        // On Windows, check the standard install location; on other platforms just use "node"
        if (OperatingSystem.IsWindows())
        {
            var knownPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs", "node.exe");
            if (File.Exists(knownPath)) return knownPath;
        }
        return "node";
    }
}
