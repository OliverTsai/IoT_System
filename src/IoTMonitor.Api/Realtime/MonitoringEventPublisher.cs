using IoTMonitor.Api.Contracts.Alerts;
using IoTMonitor.Api.Contracts.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace IoTMonitor.Api.Realtime;

public sealed class MonitoringEventPublisher(
    IHubContext<MonitoringHub, IMonitoringClient> hubContext,
    ILogger<MonitoringEventPublisher> logger)
{
    public Task PublishTelemetryAsync(TelemetryResponse telemetry)
    {
        return PublishSafelyAsync(
            () => hubContext.Clients.All.TelemetryReceived(telemetry),
            "telemetry",
            telemetry.Id);
    }

    public Task PublishAlertRaisedAsync(AlertResponse alert)
    {
        return PublishSafelyAsync(
            () => hubContext.Clients.All.AlertRaised(alert),
            "alert",
            alert.Id);
    }

    public Task PublishAlertAcknowledgedAsync(AlertResponse alert)
    {
        return PublishSafelyAsync(
            () => hubContext.Clients.All.AlertAcknowledged(alert),
            "alert acknowledgement",
            alert.Id);
    }

    private async Task PublishSafelyAsync(
        Func<Task> publish,
        string eventType,
        long eventId)
    {
        try
        {
            await publish();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to publish real-time {EventType} event {EventId}; clients will recover via REST synchronization.",
                eventType,
                eventId);
        }
    }
}
