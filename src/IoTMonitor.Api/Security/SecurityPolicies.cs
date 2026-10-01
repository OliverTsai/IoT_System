namespace IoTMonitor.Api.Security;

public static class SecurityPolicies
{
    public const string AdminOnly = "admin-only";
    public const string OperatorOrAdmin = "operator-or-admin";
    public const string WebClientCors = "web-client";
    public const string LoginRateLimit = "login";
}
