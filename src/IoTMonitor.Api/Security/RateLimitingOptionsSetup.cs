using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Security;

public sealed class RateLimitingOptionsSetup(
    IOptions<WebSecurityOptions> securityOptions) : IConfigureOptions<RateLimiterOptions>
{
    public void Configure(RateLimiterOptions options)
    {
        var security = securityOptions.Value;
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var partitionKey = string.IsNullOrEmpty(userId)
                ? $"ip:{remoteAddress}"
                : $"user:{userId}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => CreateLimiterOptions(security.GlobalPermitLimit, security));
        });
        options.AddPolicy(
            SecurityPolicies.LoginRateLimit,
            context => RateLimitPartition.GetFixedWindowLimiter(
                $"login:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => CreateLimiterOptions(security.LoginPermitLimit, security)));
        options.OnRejected = async (context, _) =>
        {
            await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many requests",
                    detail: "The request rate limit was exceeded. Try again later.")
                .ExecuteAsync(context.HttpContext);
        };
    }

    private static FixedWindowRateLimiterOptions CreateLimiterOptions(
        int permitLimit,
        WebSecurityOptions security)
    {
        return new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromSeconds(security.RateLimitWindowSeconds)
        };
    }
}
