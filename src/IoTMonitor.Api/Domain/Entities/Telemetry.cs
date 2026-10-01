namespace IoTMonitor.Api.Domain.Entities;

public sealed class Telemetry
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public decimal TemperatureCelsius { get; set; }

    public decimal HumidityPercent { get; set; }

    public DateTime RecordedAtUtc { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public Device Device { get; set; } = null!;
}
