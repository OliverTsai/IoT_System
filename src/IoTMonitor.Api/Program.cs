using IoTMonitor.Api.Data;
using IoTMonitor.Api.HealthChecks;
using IoTMonitor.Api.Messaging.Mqtt;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

app.Run();

public partial class Program;
