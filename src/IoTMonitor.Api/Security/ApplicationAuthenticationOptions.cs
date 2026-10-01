namespace IoTMonitor.Api.Security;

public sealed class ApplicationAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public int CookieLifetimeMinutes { get; init; } = 30;

    public BootstrapAdminOptions BootstrapAdmin { get; init; } = new();
}

public sealed class BootstrapAdminOptions
{
    public bool Enabled { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
