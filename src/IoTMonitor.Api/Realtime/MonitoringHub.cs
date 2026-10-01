using IoTMonitor.Api.Contracts.Alerts;
using IoTMonitor.Api.Contracts.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IoTMonitor.Api.Realtime;

public interface IMonitoringClient
{
    Task TelemetryReceived(TelemetryResponse telemetry);

    Task AlertRaised(AlertResponse alert);

    Task AlertAcknowledged(AlertResponse alert);
}

[Authorize]
public sealed class MonitoringHub : Hub<IMonitoringClient>;
