using IoTMonitor.Api.Domain.Enums;

namespace IoTMonitor.Api.Domain.Entities;

public sealed class Alert
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public AlertType Type { get; set; }

    public AlertSeverity Severity { get; set; }

    public required string Message { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }

    public Device Device { get; set; } = null!;
}
