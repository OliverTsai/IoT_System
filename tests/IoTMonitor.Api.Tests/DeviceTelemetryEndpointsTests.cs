using System.Net;
using System.Net.Http.Json;
using IoTMonitor.Api.Contracts.Common;
using IoTMonitor.Api.Contracts.Devices;
using IoTMonitor.Api.Contracts.Telemetry;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace IoTMonitor.Api.Tests;

public sealed class DeviceTelemetryEndpointsTests(PostgreSqlApiFactory factory)
    : IClassFixture<PostgreSqlApiFactory>
{
    private const string DatabaseSkipReason =
        "Set IOT_MONITOR_TEST_CONNECTION_STRING to run PostgreSQL integration tests.";

    public static bool IsDatabaseConfigured => PostgreSqlApiFactory.IsConfigured;

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task DeviceWorkflow_CreatesListsGetsAndUpdatesStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var externalId = NewExternalId();

        var createResponse = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest(externalId, "Boiler room sensor"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken);
        Assert.NotNull(created);
        Assert.Equal(externalId, created.ExternalId);
        Assert.True(created.IsActive);
        Assert.Equal($"https://localhost/api/devices/{created.Id}", createResponse.Headers.Location?.ToString());

        var list = await client.GetFromJsonAsync<PagedResponse<DeviceResponse>>(
            "/api/devices?page=1&pageSize=10",
            cancellationToken);
        Assert.NotNull(list);
        Assert.Contains(list.Items, device => device.Id == created.Id);

        var details = await client.GetFromJsonAsync<DeviceDetailsResponse>(
            $"/api/devices/{created.Id}",
            cancellationToken);
        Assert.NotNull(details);
        Assert.Null(details.LatestTelemetry);

        var statusResponse = await client.PatchAsJsonAsync(
            $"/api/devices/{created.Id}/status",
            new UpdateDeviceStatusRequest(false),
            cancellationToken);
        statusResponse.EnsureSuccessStatusCode();
        var updated = await statusResponse.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task CreateDevice_WithDuplicateExternalId_ReturnsConflictProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var request = new CreateDeviceRequest(NewExternalId(), "Packing line sensor");

        var firstResponse = await client.PostAsJsonAsync(
            "/api/devices",
            request,
            cancellationToken);
        firstResponse.EnsureSuccessStatusCode();

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/devices",
            request,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Duplicate external identifier", problem.Title);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task DeviceEndpoints_WithUnknownDevice_ReturnNotFoundProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var missingDeviceId = Guid.NewGuid();

        var deviceResponse = await client.GetAsync(
            $"/api/devices/{missingDeviceId}",
            cancellationToken);
        var telemetryResponse = await client.PostAsJsonAsync(
            $"/api/devices/{missingDeviceId}/telemetry",
            ValidTelemetryRequest(DateTimeOffset.UtcNow.AddMinutes(-1)),
            cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, deviceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, telemetryResponse.StatusCode);

        var problem = await telemetryResponse.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Device not found", problem.Title);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task CreateDevice_WithInvalidInput_ReturnsValidationProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest("invalid identifier", "   "),
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateDeviceRequest.ExternalId), problem.Errors.Keys);
        Assert.Contains(nameof(CreateDeviceRequest.Name), problem.Errors.Keys);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task TelemetryWorkflow_WritesAndQueriesLatestAndHistory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var device = await CreateDeviceAsync(client, cancellationToken);
        var firstRecordedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var secondRecordedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/telemetry",
            new CreateTelemetryRequest(24.5m, 55.2m, firstRecordedAt),
            cancellationToken);
        var secondResponse = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/telemetry",
            new CreateTelemetryRequest(25.8m, 54.1m, secondRecordedAt),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var latest = await client.GetFromJsonAsync<TelemetryResponse>(
            $"/api/devices/{device.Id}/telemetry/latest",
            cancellationToken);
        Assert.NotNull(latest);
        Assert.Equal(25.8m, latest.TemperatureCelsius);

        var details = await client.GetFromJsonAsync<DeviceDetailsResponse>(
            $"/api/devices/{device.Id}",
            cancellationToken);
        Assert.NotNull(details?.LatestTelemetry);
        Assert.Equal(latest.Id, details.LatestTelemetry.Id);

        var fromUtc = Uri.EscapeDataString(secondRecordedAt.AddSeconds(-1).ToString("O"));
        var toUtc = Uri.EscapeDataString(secondRecordedAt.AddSeconds(1).ToString("O"));
        var filteredHistory = await client.GetFromJsonAsync<PagedResponse<TelemetryResponse>>(
            $"/api/devices/{device.Id}/telemetry?fromUtc={fromUtc}&toUtc={toUtc}&page=1&pageSize=10",
            cancellationToken);
        Assert.NotNull(filteredHistory);
        Assert.Single(filteredHistory.Items);
        Assert.Equal(latest.Id, filteredHistory.Items[0].Id);

        var pagedHistory = await client.GetFromJsonAsync<PagedResponse<TelemetryResponse>>(
            $"/api/devices/{device.Id}/telemetry?page=1&pageSize=1",
            cancellationToken);
        Assert.NotNull(pagedHistory);
        Assert.Equal(2, pagedHistory.TotalCount);
        Assert.Equal(2, pagedHistory.TotalPages);
        Assert.Single(pagedHistory.Items);
        Assert.Equal(latest.Id, pagedHistory.Items[0].Id);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task TelemetryEndpoints_WithInvalidValuesOrTime_ReturnValidationProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var device = await CreateDeviceAsync(client, cancellationToken);

        var invalidValuesResponse = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/telemetry",
            new CreateTelemetryRequest(250m, -1m, DateTimeOffset.UtcNow),
            cancellationToken);
        var futureResponse = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/telemetry",
            ValidTelemetryRequest(DateTimeOffset.UtcNow.AddHours(1)),
            cancellationToken);
        var fromUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var toUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
        var invalidRangeResponse = await client.GetAsync(
            $"/api/devices/{device.Id}/telemetry?fromUtc={fromUtc}&toUtc={toUtc}",
            cancellationToken);
        var invalidPageResponse = await client.GetAsync(
            $"/api/devices/{device.Id}/telemetry?page=0",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidValuesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, futureResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRangeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);
    }

    private static async Task<DeviceResponse> CreateDeviceAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest(NewExternalId(), "Integration test sensor"),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken);
        return Assert.IsType<DeviceResponse>(device);
    }

    private static CreateTelemetryRequest ValidTelemetryRequest(DateTimeOffset recordedAtUtc)
    {
        return new CreateTelemetryRequest(23.4m, 45.6m, recordedAtUtc);
    }

    private static string NewExternalId()
    {
        return $"it-{Guid.NewGuid():N}";
    }
}
