using System.Text.Json.Serialization;
using IoTMonitor.Api.Alerts;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.HealthChecks;
using IoTMonitor.Api.Messaging.Mqtt;
using IoTMonitor.Api.Realtime;
using IoTMonitor.Api.Security;
using IoTMonitor.Api.TelemetryProcessing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var authenticationConfiguration = builder.Configuration
    .GetSection(ApplicationAuthenticationOptions.SectionName)
    .Get<ApplicationAuthenticationOptions>() ?? new ApplicationAuthenticationOptions();
var securityConfiguration = builder.Configuration
    .GetSection(WebSecurityOptions.SectionName)
    .Get<WebSecurityOptions>() ?? new WebSecurityOptions();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add(new AuthorizeFilter(
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build()));
        options.Filters.Add<ApiAntiforgeryFilter>();
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddOptions<AlertRulesOptions>()
    .Bind(builder.Configuration.GetSection(AlertRulesOptions.SectionName))
    .Validate(
        options => options.HasValidThresholdOrder(),
        "Alert warning thresholds must be inside their critical thresholds and minimums must be lower than maximums.")
    .Validate(
        options => options.HasSupportedValues(),
        "Alert temperature thresholds must be between -100 and 200, and humidity thresholds between 0 and 100.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ApplicationAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(ApplicationAuthenticationOptions.SectionName))
    .Validate(
        options => options.CookieLifetimeMinutes is >= 5 and <= 1_440,
        "Authentication cookie lifetime must be between 5 and 1440 minutes.")
    .Validate(
        options => !options.BootstrapAdmin.Enabled ||
            (UserCredentials.IsValidUsername(options.BootstrapAdmin.Username) &&
             UserCredentials.IsValidPassword(options.BootstrapAdmin.Password)),
        "Enabled bootstrap administrator configuration requires a valid username and a strong password.")
    .ValidateOnStart();

builder.Services
    .AddOptions<WebSecurityOptions>()
    .Bind(builder.Configuration.GetSection(WebSecurityOptions.SectionName))
    .Validate(
        options => options.AllowedOrigins.Length > 0 &&
            options.AllowedOrigins.All(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                !origin.Contains('*', StringComparison.Ordinal)),
        "Security allowed origins must contain explicit absolute HTTP or HTTPS origins without wildcards.")
    .Validate(
        options => options.GlobalPermitLimit is >= 10 and <= 10_000 &&
            options.LoginPermitLimit is >= 1 and <= 100 &&
            options.RateLimitWindowSeconds is >= 1 and <= 3_600,
        "Security rate limits are outside their supported ranges.")
    .ValidateOnStart();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddScoped<ApplicationCookieEvents>();
builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddHostedService<BootstrapAdminHostedService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-IoTMonitor.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.EventsType = typeof(ApplicationCookieEvents);
        options.ExpireTimeSpan = TimeSpan.FromMinutes(
            authenticationConfiguration.CookieLifetimeMinutes);
        options.SlidingExpiration = false;
    });

builder.Services
    .AddOptions<CookieAuthenticationOptions>(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((options, timeProvider) =>
        options.TimeProvider = timeProvider);

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        SecurityPolicies.AdminOnly,
        policy => policy.RequireRole(nameof(UserRole.Admin)))
    .AddPolicy(
        SecurityPolicies.OperatorOrAdmin,
        policy => policy.RequireRole(nameof(UserRole.Operator), nameof(UserRole.Admin)));

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-IoTMonitor.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        SecurityPolicies.WebClientCors,
        policy => policy
            .WithOrigins(securityConfiguration.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddRateLimiter(_ => { });
builder.Services.AddSingleton<IConfigureOptions<RateLimiterOptions>, RateLimitingOptionsSetup>();

builder.Services
    .AddOptions<MqttOptions>()
    .Bind(builder.Configuration.GetSection(MqttOptions.SectionName))
    .Validate(
        mqtt => !mqtt.Enabled ||
            (!string.IsNullOrWhiteSpace(mqtt.Host) &&
             mqtt.Port is > 0 and <= 65_535 &&
             !string.IsNullOrWhiteSpace(mqtt.ClientId) &&
             !string.IsNullOrWhiteSpace(mqtt.Username) &&
             !string.IsNullOrWhiteSpace(mqtt.Password) &&
             !string.IsNullOrWhiteSpace(mqtt.TopicFilter) &&
             mqtt.ReconnectDelaySeconds is >= 1 and <= 300 &&
             mqtt.DuplicateWindowMinutes is >= 1 and <= 1_440 &&
             mqtt.MaxTrackedMessageIds is >= 100 and <= 1_000_000),
        "Enabled MQTT configuration requires a host, valid port, client id, credentials, " +
        "and valid retry and duplicate-window limits.")
    .ValidateOnStart();

builder.Services.AddSingleton<MqttTelemetryMessageParser>();
builder.Services.AddSingleton<ITelemetryDeduplicator, TelemetryDeduplicator>();
builder.Services.AddSingleton<MqttTelemetryProcessor>();
builder.Services.AddHostedService<MqttTelemetryBackgroundService>();
builder.Services.AddSingleton<AlertRuleEvaluator>();
builder.Services.AddSingleton<MonitoringEventPublisher>();
builder.Services.AddScoped<TelemetryIngestionService>();

builder.Services.AddDbContextPool<IoTMonitorDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("IoTMonitor");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'IoTMonitor' is required. Configure it with user secrets or the " +
            "ConnectionStrings__IoTMonitor environment variable.");
    }

    options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsAssembly(typeof(IoTMonitorDbContext).Assembly.GetName().Name));
});

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("The API process is running."),
        tags: ["live", "ready"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(SecurityPolicies.WebClientCors);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    })
    .AllowAnonymous();

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    })
    .AllowAnonymous();

app.Run();

public partial class Program;
