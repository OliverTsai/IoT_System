namespace IoTMonitor.Api.Messaging.Mqtt;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; init; }

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1883;

    public string ClientId { get; init; } = "iot-monitor-api";

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string TopicFilter { get; init; } = "devices/+/telemetry";

    public int ReconnectDelaySeconds { get; init; } = 5;

    public int DuplicateWindowMinutes { get; init; } = 10;

    public int MaxTrackedMessageIds { get; init; } = 10_000;
}
