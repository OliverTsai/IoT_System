namespace IoTMonitor.Api.Messaging.Mqtt;

public sealed record MqttTelemetryMessage(
    Guid MessageId,
    string ExternalDeviceId,
    decimal TemperatureCelsius,
    decimal HumidityPercent,
    DateTime RecordedAtUtc);

public sealed record MqttTelemetryParseResult(
    MqttTelemetryMessage? Message,
    string? Error)
{
    public bool IsSuccess => Message is not null;

    public static MqttTelemetryParseResult Success(MqttTelemetryMessage message)
    {
        return new MqttTelemetryParseResult(message, null);
    }

    public static MqttTelemetryParseResult Failure(string error)
    {
        return new MqttTelemetryParseResult(null, error);
    }
}

internal sealed record MqttTelemetryPayload(
    Guid? MessageId,
    decimal? TemperatureCelsius,
    decimal? HumidityPercent,
    DateTimeOffset? RecordedAtUtc);
