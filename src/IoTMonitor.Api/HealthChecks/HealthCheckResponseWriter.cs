using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IoTMonitor.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            totalDurationMilliseconds = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMilliseconds = entry.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }
}
