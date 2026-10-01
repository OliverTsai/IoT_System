namespace IoTMonitor.Api.Security;

public sealed class WebSecurityOptions
{
    public const string SectionName = "Security";

    public string[] AllowedOrigins { get; init; } =
    [
        "http://localhost:5173",
        "https://localhost:5173"
    ];

    public int GlobalPermitLimit { get; init; } = 120;

    public int LoginPermitLimit { get; init; } = 5;

    public int RateLimitWindowSeconds { get; init; } = 60;
}
