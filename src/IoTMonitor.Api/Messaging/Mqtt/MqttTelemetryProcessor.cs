using IoTMonitor.Api.Data;
using IoTMonitor.Api.TelemetryProcessing;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitor.Api.Messaging.Mqtt;

public enum TelemetryProcessingStatus
{
    Stored,
    Duplicate,
    Rejected
}

public sealed record TelemetryProcessingResult(
    TelemetryProcessingStatus Status,
    string Detail,
    long? TelemetryId = null);

public sealed class MqttTelemetryProcessor(
    IServiceScopeFactory scopeFactory,
    MqttTelemetryMessageParser parser,
    ITelemetryDeduplicator deduplicator,
    ILogger<MqttTelemetryProcessor> logger)
{
    public async Task<TelemetryProcessingResult> ProcessAsync(
        string topic,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var parseResult = parser.Parse(topic, payload);
        if (!parseResult.IsSuccess)
        {
            logger.LogWarning(
                "Rejected MQTT telemetry on topic {Topic}: {Reason}",
                topic,
                parseResult.Error);
            return new TelemetryProcessingResult(
                TelemetryProcessingStatus.Rejected,
                parseResult.Error!);
        }

        var message = parseResult.Message!;
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IoTMonitorDbContext>();
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate => candidate.ExternalId == message.ExternalDeviceId,
            cancellationToken);

        if (device is null)
        {
            const string detail = "No device matches the external identifier in the MQTT topic.";
            logger.LogWarning(
                "Rejected MQTT telemetry for unknown device {ExternalDeviceId}",
                message.ExternalDeviceId);
            return new TelemetryProcessingResult(TelemetryProcessingStatus.Rejected, detail);
        }

        if (!device.IsActive)
        {
            const string detail = "The device is inactive.";
            logger.LogWarning(
                "Rejected MQTT telemetry for inactive device {ExternalDeviceId}",
                message.ExternalDeviceId);
            return new TelemetryProcessingResult(TelemetryProcessingStatus.Rejected, detail);
        }

        if (!deduplicator.TryReserve(message.ExternalDeviceId, message.MessageId))
        {
            logger.LogInformation(
                "Ignored duplicate MQTT telemetry {MessageId} for device {ExternalDeviceId}",
                message.MessageId,
                message.ExternalDeviceId);
            return new TelemetryProcessingResult(
                TelemetryProcessingStatus.Duplicate,
                "The MQTT message was already processed within the duplicate window.");
        }

        try
        {
            var telemetryIngestionService = scope.ServiceProvider
                .GetRequiredService<TelemetryIngestionService>();
            var result = await telemetryIngestionService.StoreAsync(
                device,
                message.TemperatureCelsius,
                message.HumidityPercent,
                message.RecordedAtUtc,
                cancellationToken);

            logger.LogDebug(
                "Stored MQTT telemetry {MessageId} for device {ExternalDeviceId}",
                message.MessageId,
                message.ExternalDeviceId);
            return new TelemetryProcessingResult(
                TelemetryProcessingStatus.Stored,
                "The MQTT telemetry was stored.",
                result.Telemetry.Id);
        }
        catch
        {
            deduplicator.Release(message.ExternalDeviceId, message.MessageId);
            throw;
        }
    }
}
