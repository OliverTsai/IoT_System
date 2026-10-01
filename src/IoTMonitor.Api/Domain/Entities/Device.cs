namespace IoTMonitor.Api.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; }

    public required string ExternalId { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Telemetry> TelemetryReadings { get; set; } = [];

    public ICollection<Alert> Alerts { get; set; } = [];
}
