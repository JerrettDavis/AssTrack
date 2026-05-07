namespace AssTrack.Api;

internal static class ApiDateTime
{
    public static DateTime Utc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? Utc(DateTime? value)
        => value is null ? null : Utc(value.Value);
}
