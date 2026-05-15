namespace AssTrack.Api.Auth;

public static class AssTrackRoles
{
    public const string Viewer = "viewer";
    public const string Operator = "operator";
    public const string Admin = "admin";
    public const string Ingest = "ingest";
}

public static class AssTrackAccessTiers
{
    public const string Community = "community";
    public const string Professional = "professional";
    public const string Enterprise = "enterprise";
    public const string Device = "device";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Enterprise;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            Community => Community,
            Professional => Professional,
            Enterprise => Enterprise,
            Device => Device,
            _ => Enterprise
        };
    }
}

public static class AssTrackClaimTypes
{
    public const string AccessTier = "asstrack:tier";
}

public static class AssTrackPolicies
{
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
    public const string Admin = "Admin";
    public const string Ingest = "Ingest";
    public const string Enterprise = "Enterprise";
}
