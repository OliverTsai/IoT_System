namespace IoTMonitor.DeviceSimulator;

internal sealed record SimulatorTelemetryPayload(
    Guid MessageId,
    decimal TemperatureCelsius,
    decimal HumidityPercent,
    DateTimeOffset RecordedAtUtc);
