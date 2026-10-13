using System.Text.Json;

namespace IoTMonitor.Api.Messaging.Protocols;

public sealed record DeviceProtocolFieldDescriptor(
    string Key,
    string DisplayName,
    string InputType,
    bool IsRequired,
    decimal? Minimum = null,
    decimal? Maximum = null,
    string? Placeholder = null);

public sealed record DeviceProtocolDescriptor(
    string Key,
    string DisplayName,
    IReadOnlyList<DeviceProtocolFieldDescriptor> Fields,
    bool IsDefault = false);

public sealed record DeviceProtocolSettingsValidationResult(
    string? NormalizedSettingsJson,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0 && NormalizedSettingsJson is not null;

    public static DeviceProtocolSettingsValidationResult Valid(string normalizedSettingsJson)
    {
        return new DeviceProtocolSettingsValidationResult(
            normalizedSettingsJson,
            new Dictionary<string, string[]>());
    }

    public static DeviceProtocolSettingsValidationResult Invalid(
        string key,
        params string[] errors)
    {
        return new DeviceProtocolSettingsValidationResult(
            null,
            new Dictionary<string, string[]> { [key] = errors });
    }
}

public interface IDeviceTelemetryProtocolAdapter
{
    DeviceProtocolDescriptor Descriptor { get; }

    bool IsEnabled { get; }

    DeviceProtocolSettingsValidationResult ValidateSettings(JsonElement? settings);

    Task RunAsync(CancellationToken cancellationToken);
}
