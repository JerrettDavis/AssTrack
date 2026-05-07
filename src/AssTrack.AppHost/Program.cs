var builder = DistributedApplication.CreateBuilder(args);

const string apiUrl = "http://localhost:5019";
const string frontendUrl = "http://localhost:5174";
const string frontendLoopbackUrl = "http://127.0.0.1:5174";

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
EnsureLocalEnvironmentFile(repoRoot);
LoadEnvironmentFile(Path.Combine(repoRoot, ".env"));

var apiKey = GetEnvironmentValue("ASSTRACK_API_KEY") ?? "local-dev-key-asstrack";
var ingestApiKey = GetEnvironmentValue("ASSTRACK_INGEST_API_KEY") ?? apiKey;
var connectionString = GetEnvironmentValue("ASSTRACK_CONNECTION_STRING") ?? $"Data Source={Path.Combine(repoRoot, "asstrack-dev.db")}";
var frontendDirectory = Path.Combine(repoRoot, "frontend");
EnsureNpmOnPath();

var api = builder.AddProject<Projects.AssTrack_Api>("api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Auth__ApiKey", apiKey)
    .WithEnvironment("Auth__IngestApiKey", ingestApiKey)
    .WithEnvironment("ConnectionStrings__DefaultConnection", connectionString)
    .WithEnvironment("Cors__AllowedOrigins__0", frontendUrl)
    .WithEnvironment("Cors__AllowedOrigins__1", frontendLoopbackUrl)
    .WithEnvironment("Swagger__Enabled", "true")
    .WithEnvironment("RateLimiting__IngestPermitLimit", "240")
    .WithEnvironment("RateLimiting__IngestWindowSeconds", "60")
    .WithExternalHttpEndpoints();

var bridge = builder.AddProject<Projects.AssTrack_BridgeGateway>("bridge-gateway")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("BridgeGateway__AssTrackBaseUrl", apiUrl)
    .WithEnvironment("BridgeGateway__IngestApiKey", ingestApiKey)
    .WithEnvironment("BridgeGateway__OperatorApiKey", apiKey)
    .WithEnvironment("BridgeGateway__DryRun", GetEnvironmentValue("ASSTRACK_BRIDGE_DRY_RUN") ?? "false")
    .WithEnvironment("BridgeGateway__Feeds__generic__Enabled", "false")
    .WithEnvironment("BridgeGateway__Feeds__generic__FeedId", "00000000-0000-0000-0000-000000000000")
    .WithEnvironment("BridgeGateway__Feeds__generic__Provider", "generic-webhook")
    .WithEnvironment("BridgeGateway__Feeds__generic__SharedSecret", "change-me")
    .WithEnvironment("BridgeGateway__Feeds__generic__DefaultTags", "bridge, generic")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.AddViteApp("frontend", frontendDirectory)
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5174;
        endpoint.TargetPort = 5174;
        endpoint.UriScheme = "http";
        endpoint.IsProxied = false;
        endpoint.IsExternal = true;
    })
    .WithNpm()
    .WithRunScript("dev")
    .WithEnvironment("PORT", "5174")
    .WithEnvironment("VITE_DEV_API_KEY", apiKey)
    .WithEnvironment("VITE_E2E_PROXY_TARGET", apiUrl)
    .WithEnvironment("VITE_BRIDGE_PROXY_TARGET", "http://localhost:5056")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();

static void EnsureLocalEnvironmentFile(string repoRoot)
{
    var localEnvPath = Path.Combine(repoRoot, ".env");
    if (File.Exists(localEnvPath))
    {
        return;
    }

    var exampleEnvPath = Path.Combine(repoRoot, ".env.example");
    var content = File.Exists(exampleEnvPath)
        ? File.ReadAllText(exampleEnvPath)
        : """
          ASSTRACK_API_KEY=local-dev-key-asstrack
          ASSTRACK_INGEST_API_KEY=
          ASSTRACK_BRIDGE_DRY_RUN=false
          """;

    File.WriteAllText(localEnvPath, content.Replace("ASSTRACK_API_KEY=change-me", "ASSTRACK_API_KEY=local-dev-key-asstrack"));
}

static void LoadEnvironmentFile(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static string? GetEnvironmentValue(string key)
{
    var value = Environment.GetEnvironmentVariable(key);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static void EnsureNpmOnPath()
{
    var npmPath = ResolveExecutable("npm");
    if (npmPath is null)
    {
        throw new InvalidOperationException(
            "npm was not found. Install Node.js 20.19+ and ensure npm is available on PATH, " +
            "or install Node.js in the standard location for your OS.");
    }

    SetChildProcessPath(npmPath);
}

static string? ResolveExecutable(string command)
{
    foreach (var directory in GetSearchDirectories())
    {
        foreach (var fileName in GetExecutableFileNames(command))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    return null;
}

static IEnumerable<string> GetSearchDirectories()
{
    var path = Environment.GetEnvironmentVariable("PATH") ?? Environment.GetEnvironmentVariable("Path") ?? string.Empty;
    foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        yield return directory;
    }

    if (OperatingSystem.IsWindows())
    {
        foreach (var root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            yield return Path.Combine(root, "nodejs");
            yield return Path.Combine(root, "Programs", "nodejs");
        }
    }
    else
    {
        yield return "/usr/local/bin";
        yield return "/opt/homebrew/bin";
        yield return "/usr/bin";
    }
}

static IEnumerable<string> GetExecutableFileNames(string command)
{
    if (!OperatingSystem.IsWindows())
    {
        yield return command;
        yield break;
    }

    if (Path.HasExtension(command))
    {
        yield return command;
        yield break;
    }

    foreach (var extension in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        yield return command + extension.ToLowerInvariant();
        yield return command + extension.ToUpperInvariant();
    }
}

static void SetChildProcessPath(string npmPath)
{
    var directories = new List<string> { Path.GetDirectoryName(npmPath)! };
    var dotnetPath = ResolveExecutable("dotnet");
    if (dotnetPath is not null)
    {
        directories.Add(Path.GetDirectoryName(dotnetPath)!);
    }

    if (OperatingSystem.IsWindows())
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        directories.Add(Path.Combine(systemRoot, "system32"));
        directories.Add(systemRoot);
        directories.Add(Path.Combine(systemRoot, "System32", "Wbem"));
        directories.Add(Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0"));
    }
    else
    {
        directories.AddRange(["/usr/local/bin", "/opt/homebrew/bin", "/usr/bin", "/bin"]);
    }

    var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    var updatedPath = string.Join(Path.PathSeparator, directories
        .Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        .Distinct(comparison));

    Environment.SetEnvironmentVariable("PATH", updatedPath);
    if (OperatingSystem.IsWindows())
    {
        Environment.SetEnvironmentVariable("Path", updatedPath);
    }
}

static string FindRepoRoot(string startDirectory)
{
    var directory = new DirectoryInfo(startDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "AssTrack.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return startDirectory;
}
