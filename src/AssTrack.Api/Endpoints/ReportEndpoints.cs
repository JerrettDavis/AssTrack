using AssTrack.Api;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/reports").RequireAuthorization("Operator");

        reports.MapGet("/utilization", async (
            ReportRepository repository,
            DateTimeOffset? from,
            DateTimeOffset? to,
            Guid? assetId,
            Guid? deviceId,
            CancellationToken cancellationToken) =>
        {
            var end = to?.UtcDateTime ?? DateTime.UtcNow;
            var start = from?.UtcDateTime ?? end.AddDays(-7);
            if (start >= end)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["range"] = ["Report start must be before end."]
                });
            }

            var maxWindow = TimeSpan.FromDays(31);
            if (end - start > maxWindow)
            {
                start = end - maxWindow;
            }

            var report = await repository.GetUtilizationAsync(
                start,
                end,
                assetId,
                deviceId,
                cancellationToken);

            return Results.Ok(report with
            {
                From = ApiDateTime.Utc(report.From),
                To = ApiDateTime.Utc(report.To),
                GeneratedAt = ApiDateTime.Utc(report.GeneratedAt)
            });
        });

        return group;
    }
}
