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

    private static string ResolveNodeCommand()
    {
        var knownPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        return File.Exists(knownPath) ? knownPath : "node";
    }
}
