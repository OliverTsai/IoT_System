using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTMonitor.Api.Contracts.Alerts;
using IoTMonitor.Api.Contracts.Common;
using IoTMonitor.Api.Contracts.Devices;
using IoTMonitor.Api.Contracts.Telemetry;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.Tests.Infrastructure;

namespace IoTMonitor.Api.Tests;

public sealed class AlertEndpointsTests(PostgreSqlApiFactory factory)
    : IClassFixture<PostgreSqlApiFactory>
{
    private const string DatabaseSkipReason =
        "Set IOT_MONITOR_TEST_CONNECTION_STRING to run PostgreSQL integration tests.";

    public static bool IsDatabaseConfigured => PostgreSqlApiFactory.IsConfigured;

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task OutOfRangeTelemetry_CreatesQueryableAlert_ThatOperatorCanAcknowledge()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var device = await CreateDeviceAsync(admin, cancellationToken);

        var safeResponse = await admin.PostAsJsonAsync(
            $"/api/devices/{device.Id}/telemetry",
            new CreateTelemetryRequest(22m, 50m, factory.Clock.GetUtcNow().AddMinutes(-2)),
            cancellationToken);
        var criticalResponse = await admin.PostAsJsonAsync(
            $"/api/devices/{device.Id}/telemetry",
            new CreateTelemetryRequest(46m, 50m, factory.Clock.GetUtcNow().AddMinutes(-1)),
            cancellationToken);
        safeResponse.EnsureSuccessStatusCode();
        criticalResponse.EnsureSuccessStatusCode();

        var openAlerts = await admin.GetFromJsonAsync<PagedResponse<AlertResponse>>(
            $"/api/alerts?deviceId={device.Id}&acknowledged=false&page=1&pageSize=10",
            ResponseJsonOptions,
            cancellationToken);
        Assert.NotNull(openAlerts);
        var alert = Assert.Single(openAlerts.Items);
        Assert.Equal(AlertType.TemperatureOutOfRange, alert.Type);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(device.ExternalId, alert.DeviceExternalId);
        Assert.Null(alert.AcknowledgedAtUtc);

        using var viewer = await factory.CreateAuthenticatedClientAsync(
            UserRole.Viewer,
            cancellationToken);
        var forbidden = await viewer.PatchAsync(
            $"/api/alerts/{alert.Id}/acknowledge",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var operatorClient = await factory.CreateAuthenticatedClientAsync(
            UserRole.Operator,
            cancellationToken);
        var acknowledgedResponse = await operatorClient.PatchAsync(
            $"/api/alerts/{alert.Id}/acknowledge",
            null,
            cancellationToken);
        acknowledgedResponse.EnsureSuccessStatusCode();
        var acknowledged = await acknowledgedResponse.Content
            .ReadFromJsonAsync<AlertResponse>(ResponseJsonOptions, cancellationToken);
        Assert.NotNull(acknowledged?.AcknowledgedAtUtc);

        var repeatedResponse = await operatorClient.PatchAsync(
            $"/api/alerts/{alert.Id}/acknowledge",
            null,
            cancellationToken);
        repeatedResponse.EnsureSuccessStatusCode();
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<AlertResponse>(
            ResponseJsonOptions,
            cancellationToken);
        Assert.Equal(acknowledged.AcknowledgedAtUtc, repeated?.AcknowledgedAtUtc);

        var acknowledgedAlerts = await admin.GetFromJsonAsync<PagedResponse<AlertResponse>>(
            $"/api/alerts?deviceId={device.Id}&acknowledged=true&page=1&pageSize=10",
            ResponseJsonOptions,
            cancellationToken);
        Assert.NotNull(acknowledgedAlerts);
        Assert.Contains(acknowledgedAlerts.Items, candidate => candidate.Id == alert.Id);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task AlertEndpoints_ValidateFiltersAndUnknownIdentifier()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);

        var invalidPage = await client.GetAsync(
            "/api/alerts?page=0",
            cancellationToken);
        var invalidSeverity = await client.GetAsync(
            "/api/alerts?severity=not-a-severity",
            cancellationToken);
        var missing = await client.PatchAsync(
            "/api/alerts/9223372036854775807/acknowledge",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSeverity.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<DeviceResponse> CreateDeviceAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest($"alert-it-{Guid.NewGuid():N}", "Alert integration sensor"),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken);
        return Assert.IsType<DeviceResponse>(device);
    }
}
