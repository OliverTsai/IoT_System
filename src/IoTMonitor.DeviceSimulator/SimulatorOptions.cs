using System.Globalization;
using System.Text.RegularExpressions;

namespace IoTMonitor.DeviceSimulator;

internal sealed class SimulatorOptions
{
    private static readonly Regex DevicePrefixPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._:-]*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public required string MqttHost { get; init; }

    public required int MqttPort { get; init; }

    public required string MqttUsername { get; init; }

    public required string MqttPassword { get; init; }

    public required string MqttClientId { get; init; }

    public required string DeviceIdPrefix { get; init; }

    public required int DeviceCount { get; init; }

    public required int IntervalSeconds { get; init; }

    public required int MessagesPerDevice { get; init; }

    public required int ReconnectDelaySeconds { get; init; }

    public static SimulatorOptions FromEnvironment()
    {
        var prefix = GetOptional("SIMULATOR_DEVICE_PREFIX", "sim-device");
        if (prefix.Length > 95 || !DevicePrefixPattern.IsMatch(prefix))
        {
            throw new InvalidOperationException(
                "SIMULATOR_DEVICE_PREFIX must be 1-95 characters and contain only letters, " +
                "numbers, '.', '_', ':', and '-'.");
        }

        return new SimulatorOptions
        {
            MqttHost = GetOptional("MQTT_HOST", "localhost"),
            MqttPort = GetInteger("MQTT_PORT", 1883, 1, 65_535),
            MqttUsername = GetRequired("MQTT_USERNAME"),
            MqttPassword = GetRequired("MQTT_PASSWORD"),
            MqttClientId = GetOptional(
                "MQTT_CLIENT_ID",
                $"iot-monitor-simulator-{Environment.ProcessId}"),
            DeviceIdPrefix = prefix,
            DeviceCount = GetInteger("SIMULATOR_DEVICE_COUNT", 1, 1, 1_000),
            IntervalSeconds = GetInteger("SIMULATOR_INTERVAL_SECONDS", 5, 1, 3_600),
            MessagesPerDevice = GetInteger(
                "SIMULATOR_MESSAGES_PER_DEVICE",
                0,
                0,
                1_000_000),
            ReconnectDelaySeconds = GetInteger("MQTT_RECONNECT_DELAY_SECONDS", 5, 1, 300)
        };
    }

    public string GetExternalDeviceId(int oneBasedIndex)
    {
        return $"{DeviceIdPrefix}-{oneBasedIndex:000}";
    }

    private static string GetRequired(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Environment variable '{name}' is required.");
        }

        return value;
    }

    private static string GetOptional(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int GetInteger(
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new InvalidOperationException(
                $"Environment variable '{name}' must be an integer from {minimum} to {maximum}.");
        }

        return parsed;
    }
}
