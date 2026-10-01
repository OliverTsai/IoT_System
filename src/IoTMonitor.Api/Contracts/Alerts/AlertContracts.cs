using System.ComponentModel.DataAnnotations;
using IoTMonitor.Api.Domain.Enums;

namespace IoTMonitor.Api.Contracts.Alerts;

public sealed record AlertResponse(
    long Id,
    Guid DeviceId,
    string DeviceExternalId,
    string DeviceName,
    AlertType Type,
    AlertSeverity Severity,
    string Message,
    DateTime OccurredAtUtc,
    DateTime? AcknowledgedAtUtc);

public sealed class AlertListQuery
{
    public Guid? DeviceId { get; init; }

    public AlertType? Type { get; init; }

    public AlertSeverity? Severity { get; init; }

    public bool? Acknowledged { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
