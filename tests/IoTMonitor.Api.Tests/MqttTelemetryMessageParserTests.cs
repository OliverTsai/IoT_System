using System.Text;
using System.Text.Json;
using IoTMonitor.Api.Messaging.Mqtt;

namespace IoTMonitor.Api.Tests;

public sealed class MqttTelemetryMessageParserTests
{
    private readonly MqttTelemetryMessageParser _parser = new(TimeProvider.System);

    [Fact]
    public void Parse_WithValidTopicAndPayload_ReturnsNormalizedMessage()
    {
        var messageId = Guid.NewGuid();
        var recordedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var payload = CreatePayload(messageId, 24.5m, 55.2m, recordedAt);

        var result = _parser.Parse("devices/factory-01/telemetry", payload);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Message);
        Assert.Equal(messageId, result.Message.MessageId);
        Assert.Equal("factory-01", result.Message.ExternalDeviceId);
        Assert.Equal(24.5m, result.Message.TemperatureCelsius);
        Assert.Equal(55.2m, result.Message.HumidityPercent);
        Assert.Equal(DateTimeKind.Utc, result.Message.RecordedAtUtc.Kind);
    }

    [Theory]
    [InlineData("devices/factory-01")]
    [InlineData("device/factory-01/telemetry")]
    [InlineData("devices/invalid id/telemetry")]
    [InlineData("devices/factory-01/telemetry/extra")]
    public void Parse_WithInvalidTopic_ReturnsFailure(string topic)
    {
        var payload = CreatePayload(
            Guid.NewGuid(),
            24m,
            50m,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = _parser.Parse(topic, payload);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_WithMalformedJson_ReturnsFailure()
    {
        var payload = Encoding.UTF8.GetBytes("{not-json}");

        var result = _parser.Parse("devices/factory-01/telemetry", payload);

        Assert.False(result.IsSuccess);
        Assert.Equal("The MQTT payload is not valid JSON telemetry.", result.Error);
    }

    [Theory]
    [InlineData(-101, 50)]
    [InlineData(201, 50)]
    [InlineData(20, -1)]
    [InlineData(20, 101)]
    public void Parse_WithOutOfRangeMeasurements_ReturnsFailure(
        decimal temperature,
        decimal humidity)
    {
        var payload = CreatePayload(
            Guid.NewGuid(),
            temperature,
            humidity,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = _parser.Parse("devices/factory-01/telemetry", payload);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parse_WithFutureRecordedTime_ReturnsFailure()
    {
        var payload = CreatePayload(
            Guid.NewGuid(),
            20m,
            50m,
            DateTimeOffset.UtcNow.AddHours(1));

        var result = _parser.Parse("devices/factory-01/telemetry", payload);

        Assert.False(result.IsSuccess);
        Assert.Contains("five minutes", result.Error);
    }

    private static byte[] CreatePayload(
        Guid messageId,
        decimal temperature,
        decimal humidity,
        DateTimeOffset recordedAt)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            messageId,
            temperatureCelsius = temperature,
            humidityPercent = humidity,
            recordedAtUtc = recordedAt
        });
    }
}
