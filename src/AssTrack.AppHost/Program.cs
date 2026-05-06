var builder = DistributedApplication.CreateBuilder(args);

const string apiUrl = "http://localhost:5019";
const string frontendUrl = "http://localhost:5174";
const string frontendLoopbackUrl = "http://127.0.0.1:5174";

var apiKey = Environment.GetEnvironmentVariable("ASSTRACK_API_KEY") ?? "local-dev-key-asstrack";
var ingestApiKey = Environment.GetEnvironmentVariable("ASSTRACK_INGEST_API_KEY") ?? apiKey;
var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var connectionString = Environment.GetEnvironmentVariable("ASSTRACK_CONNECTION_STRING") ?? $"Data Source={Path.Combine(repoRoot, "asstrack-dev.db")}";
var frontendDirectory = Path.Combine(repoRoot, "frontend");
var frontendLauncher = Path.Combine(frontendDirectory, "start-vite.mjs");
var nodeExecutable = Environment.GetEnvironmentVariable("NODE_EXECUTABLE")
    ?? (OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe") : "node");

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
    .WithEnvironment("BridgeGateway__DryRun", Environment.GetEnvironmentVariable("ASSTRACK_BRIDGE_DRY_RUN") ?? "false")
    .WithEnvironment("BridgeGateway__Feeds__generic__Enabled", "false")
    .WithEnvironment("BridgeGateway__Feeds__generic__FeedId", "00000000-0000-0000-0000-000000000000")
    .WithEnvironment("BridgeGateway__Feeds__generic__Provider", "generic-webhook")
    .WithEnvironment("BridgeGateway__Feeds__generic__SharedSecret", "change-me")
    .WithEnvironment("BridgeGateway__Feeds__generic__DefaultTags", "bridge, generic")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.AddExecutable("frontend", nodeExecutable, frontendDirectory, frontendLauncher, "--host", "127.0.0.1", "--port", "5174")
    .WithEnvironment("VITE_DEV_API_KEY", apiKey)
    .WithEnvironment("VITE_E2E_PROXY_TARGET", apiUrl)
    .WithEnvironment("VITE_BRIDGE_PROXY_TARGET", "http://localhost:5056")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 5174, targetPort: 5174, name: "http", isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();

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
