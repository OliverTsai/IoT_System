using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IoTMonitor.Api.Messaging.Protocols;

public sealed class DeviceProtocolRegistry
{
    private const int MaximumSettingsBytes = 16 * 1024;
    private static readonly Regex ProtocolKeyPattern = new(
        "^[a-z0-9][a-z0-9-]{0,49}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private readonly IReadOnlyDictionary<string, IDeviceTelemetryProtocolAdapter> _adapters;

    public DeviceProtocolRegistry(IEnumerable<IDeviceTelemetryProtocolAdapter> adapters)
    {
        var registeredAdapters = new Dictionary<string, IDeviceTelemetryProtocolAdapter>(
            StringComparer.Ordinal);

        foreach (var adapter in adapters)
        {
            var key = adapter.Descriptor.Key;
            if (!ProtocolKeyPattern.IsMatch(key))
            {
                throw new InvalidOperationException(
                    $"Device protocol key '{key}' must be a lowercase kebab-case identifier.");
            }

            if (!registeredAdapters.TryAdd(key, adapter))
            {
                throw new InvalidOperationException(
                    $"Device protocol key '{key}' is registered more than once.");
            }
        }

        if (registeredAdapters.Count == 0)
        {
            throw new InvalidOperationException("At least one device protocol adapter is required.");
        }

        _adapters = registeredAdapters;
        Descriptors = registeredAdapters.Values
            .Select(adapter => adapter.Descriptor)
            .OrderBy(descriptor => descriptor.DisplayName, StringComparer.Ordinal)
            .ToArray();
        Adapters = registeredAdapters.Values.ToArray();
    }

    public IReadOnlyList<DeviceProtocolDescriptor> Descriptors { get; }

    public IReadOnlyList<IDeviceTelemetryProtocolAdapter> Adapters { get; }

    public bool TryGetAdapter(
        string protocolKey,
        out IDeviceTelemetryProtocolAdapter? adapter)
    {
        return _adapters.TryGetValue(protocolKey, out adapter);
    }

    public DeviceProtocolSettingsValidationResult ValidateSettings(
        IDeviceTelemetryProtocolAdapter adapter,
        JsonElement? settings)
    {
        if (settings is { } value &&
            value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined &&
            Encoding.UTF8.GetByteCount(value.GetRawText()) > MaximumSettingsBytes)
        {
            return DeviceProtocolSettingsValidationResult.Invalid(
                "ProtocolSettings",
                $"ProtocolSettings cannot exceed {MaximumSettingsBytes} UTF-8 bytes.");
        }

        return adapter.ValidateSettings(settings);
    }
}
