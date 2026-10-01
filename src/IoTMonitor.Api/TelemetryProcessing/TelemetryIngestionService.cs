using IoTMonitor.Api.Alerts;
using IoTMonitor.Api.Contracts.Alerts;
using IoTMonitor.Api.Contracts.Telemetry;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Realtime;

namespace IoTMonitor.Api.TelemetryProcessing;

public sealed record TelemetryIngestionResult(
    TelemetryResponse Telemetry,
    IReadOnlyList<AlertResponse> Alerts);

public sealed class TelemetryIngestionService(
    IoTMonitorDbContext dbContext,
    AlertRuleEvaluator alertRuleEvaluator,
    MonitoringEventPublisher eventPublisher,
    TimeProvider timeProvider)
{
    public async Task<TelemetryIngestionResult> StoreAsync(
        Device device,
        decimal temperatureCelsius,
        decimal humidityPercent,
        DateTime recordedAtUtc,
        CancellationToken cancellationToken)
    {
        var telemetry = new Telemetry
        {
            DeviceId = device.Id,
            TemperatureCelsius = temperatureCelsius,
            HumidityPercent = humidityPercent,
            RecordedAtUtc = recordedAtUtc,
            ReceivedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Telemetry.Add(telemetry);

        var alerts = alertRuleEvaluator
            .Evaluate(temperatureCelsius, humidityPercent)
            .Select(candidate => new Alert
            {
                DeviceId = device.Id,
                Type = candidate.Type,
                Severity = candidate.Severity,
                Message = candidate.Message,
                OccurredAtUtc = recordedAtUtc
            })
            .ToList();
        dbContext.Alerts.AddRange(alerts);

        await dbContext.SaveChangesAsync(cancellationToken);

        var telemetryResponse = new TelemetryResponse(
            telemetry.Id,
            telemetry.DeviceId,
            telemetry.TemperatureCelsius,
            telemetry.HumidityPercent,
            telemetry.RecordedAtUtc,
            telemetry.ReceivedAtUtc);
        var alertResponses = alerts
            .Select(alert => ToResponse(alert, device))
            .ToList();

        await eventPublisher.PublishTelemetryAsync(telemetryResponse);
        foreach (var alert in alertResponses)
        {
            await eventPublisher.PublishAlertRaisedAsync(alert);
        }

        return new TelemetryIngestionResult(telemetryResponse, alertResponses);
    }

    private static AlertResponse ToResponse(Alert alert, Device device)
    {
        return new AlertResponse(
            alert.Id,
            alert.DeviceId,
            device.ExternalId,
            device.Name,
            alert.Type,
            alert.Severity,
            alert.Message,
            alert.OccurredAtUtc,
            alert.AcknowledgedAtUtc);
    }
}
