using System.Text.Json;
using IoTMonitor.Api.Messaging.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Tests;

public sealed class MqttTelemetryProtocolAdapterTests
{
    private readonly MqttTelemetryProtocolAdapter _adapter = new(
        Options.Create(new MqttOptions()),
        null!,
        NullLogger<MqttTelemetryProtocolAdapter>.Instance);

    [Fact]
    public void ValidateSettings_WithNoSettings_ReturnsCanonicalEmptyObject()
    {
        var result = _adapter.ValidateSettings(null);

        Assert.Equal("mqtt", _adapter.Descriptor.Key);
        Assert.True(_adapter.Descriptor.IsDefault);
        Assert.True(result.IsValid);
        Assert.Equal("{}", result.NormalizedSettingsJson);
    }

    [Fact]
    public void ValidateSettings_WithPerDeviceValues_ReturnsValidationError()
    {
        using var document = JsonDocument.Parse("""{"host":"not-used.example"}""");

        var result = _adapter.ValidateSettings(document.RootElement);

        Assert.False(result.IsValid);
        Assert.Contains("ProtocolSettings", result.Errors.Keys);
    }
}
