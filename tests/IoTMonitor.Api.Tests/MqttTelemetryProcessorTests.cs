using System.Net.Http.Json;
using System.Text.Json;
using IoTMonitor.Api.Contracts.Common;
using IoTMonitor.Api.Contracts.Devices;
using IoTMonitor.Api.Contracts.Telemetry;
using IoTMonitor.Api.Messaging.Mqtt;
using IoTMonitor.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace IoTMonitor.Api.Tests;

public sealed class MqttTelemetryProcessorTests(PostgreSqlApiFactory factory)
    : IClassFixture<PostgreSqlApiFactory>
{
    private const string DatabaseSkipReason =
        "Set IOT_MONITOR_TEST_CONNECTION_STRING to run PostgreSQL integration tests.";

    public static bool IsDatabaseConfigured => PostgreSqlApiFactory.IsConfigured;

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task ProcessAsync_WithValidMessage_StoresTelemetryVisibleThroughApi()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var device = await CreateDeviceAsync(client, cancellationToken);
        var processor = factory.Services.GetRequiredService<MqttTelemetryProcessor>();
        var payload = CreatePayload(Guid.NewGuid(), 26.4m, 48.2m);

        var result = await processor.ProcessAsync(
            $"devices/{device.ExternalId}/telemetry",
            payload,
            cancellationToken);

        Assert.Equal(TelemetryProcessingStatus.Stored, result.Status);
        Assert.NotNull(result.TelemetryId);

        var latest = await client.GetFromJsonAsync<TelemetryResponse>(
            $"/api/devices/{device.Id}/telemetry/latest",
            cancellationToken);
        Assert.NotNull(latest);
        Assert.Equal(result.TelemetryId, latest.Id);
        Assert.Equal(26.4m, latest.TemperatureCelsius);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task ProcessAsync_WithRepeatedMessageId_StoresOnlyOneReading()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var device = await CreateDeviceAsync(client, cancellationToken);
        var processor = factory.Services.GetRequiredService<MqttTelemetryProcessor>();
        var payload = CreatePayload(Guid.NewGuid(), 22.1m, 60.5m);
        var topic = $"devices/{device.ExternalId}/telemetry";

        var first = await processor.ProcessAsync(topic, payload, cancellationToken);
        var duplicate = await processor.ProcessAsync(topic, payload, cancellationToken);

        Assert.Equal(TelemetryProcessingStatus.Stored, first.Status);
        Assert.Equal(TelemetryProcessingStatus.Duplicate, duplicate.Status);

        var history = await client.GetFromJsonAsync<PagedResponse<TelemetryResponse>>(
            $"/api/devices/{device.Id}/telemetry?page=1&pageSize=10",
            cancellationToken);
        Assert.NotNull(history);
        Assert.Equal(1, history.TotalCount);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task ProcessAsync_WithInvalidOrUnknownDeviceMessage_RejectsWithoutWriting()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var device = await CreateDeviceAsync(client, cancellationToken);
        var processor = factory.Services.GetRequiredService<MqttTelemetryProcessor>();
        var invalidPayload = CreatePayload(Guid.NewGuid(), 500m, 50m);
        var validPayload = CreatePayload(Guid.NewGuid(), 20m, 50m);

        var invalid = await processor.ProcessAsync(
            $"devices/{device.ExternalId}/telemetry",
            invalidPayload,
            cancellationToken);
        var unknown = await processor.ProcessAsync(
            "devices/not-registered/telemetry",
            validPayload,
            cancellationToken);

        Assert.Equal(TelemetryProcessingStatus.Rejected, invalid.Status);
        Assert.Equal(TelemetryProcessingStatus.Rejected, unknown.Status);

        var history = await client.GetFromJsonAsync<PagedResponse<TelemetryResponse>>(
            $"/api/devices/{device.Id}/telemetry?page=1&pageSize=10",
            cancellationToken);
        Assert.NotNull(history);
        Assert.Equal(0, history.TotalCount);
    }

    private static async Task<DeviceResponse> CreateDeviceAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest($"mqtt-it-{Guid.NewGuid():N}", "MQTT integration sensor"),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var device = await response.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken);
        return Assert.IsType<DeviceResponse>(device);
    }

    private static byte[] CreatePayload(
        Guid messageId,
        decimal temperature,
        decimal humidity)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            messageId,
            temperatureCelsius = temperature,
            humidityPercent = humidity,
            recordedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
    }
}
