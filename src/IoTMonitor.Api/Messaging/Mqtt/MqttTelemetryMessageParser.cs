using System.Text.Json;
using System.Text.RegularExpressions;

namespace IoTMonitor.Api.Messaging.Mqtt;

public sealed class MqttTelemetryMessageParser(TimeProvider timeProvider)
{
    private const int MaximumPayloadBytes = 4 * 1024;
    private const string TopicPrefix = "devices";
    private const string TopicSuffix = "telemetry";
    private static readonly Regex ExternalIdPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._:-]*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public MqttTelemetryParseResult Parse(string topic, ReadOnlyMemory<byte> payload)
    {
        if (!TryGetExternalDeviceId(topic, out var externalDeviceId, out var topicError))
        {
            return MqttTelemetryParseResult.Failure(topicError);
        }

        if (payload.IsEmpty)
        {
            return MqttTelemetryParseResult.Failure("The MQTT payload is empty.");
        }

        if (payload.Length > MaximumPayloadBytes)
        {
            return MqttTelemetryParseResult.Failure(
                $"The MQTT payload exceeds the {MaximumPayloadBytes}-byte limit.");
        }

        MqttTelemetryPayload? deserialized;

        try
        {
            deserialized = JsonSerializer.Deserialize<MqttTelemetryPayload>(
                payload.Span,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return MqttTelemetryParseResult.Failure("The MQTT payload is not valid JSON telemetry.");
        }

        if (deserialized is null)
        {
            return MqttTelemetryParseResult.Failure("The MQTT payload is empty JSON.");
        }

        if (!deserialized.MessageId.HasValue || deserialized.MessageId == Guid.Empty)
        {
            return MqttTelemetryParseResult.Failure("MessageId must be a non-empty UUID.");
        }

        if (!deserialized.TemperatureCelsius.HasValue ||
            deserialized.TemperatureCelsius is < -100m or > 200m)
        {
            return MqttTelemetryParseResult.Failure(
                "TemperatureCelsius must be between -100 and 200.");
        }

        if (!deserialized.HumidityPercent.HasValue ||
            deserialized.HumidityPercent is < 0m or > 100m)
        {
            return MqttTelemetryParseResult.Failure(
                "HumidityPercent must be between 0 and 100.");
        }

        if (!deserialized.RecordedAtUtc.HasValue)
        {
            return MqttTelemetryParseResult.Failure("RecordedAtUtc is required.");
        }

        var recordedAtUtc = deserialized.RecordedAtUtc.Value.ToUniversalTime();
        if (recordedAtUtc > timeProvider.GetUtcNow().AddMinutes(5))
        {
            return MqttTelemetryParseResult.Failure(
                "RecordedAtUtc cannot be more than five minutes in the future.");
        }

        return MqttTelemetryParseResult.Success(new MqttTelemetryMessage(
            deserialized.MessageId.Value,
            externalDeviceId,
            deserialized.TemperatureCelsius.Value,
            deserialized.HumidityPercent.Value,
            recordedAtUtc.UtcDateTime));
    }

    private static bool TryGetExternalDeviceId(
        string topic,
        out string externalDeviceId,
        out string error)
    {
        externalDeviceId = string.Empty;
        error = string.Empty;

        var segments = topic.Split('/');
        if (segments.Length != 3 ||
            segments[0] != TopicPrefix ||
            segments[2] != TopicSuffix)
        {
            error = "The topic must match 'devices/{externalDeviceId}/telemetry'.";
            return false;
        }

        externalDeviceId = segments[1];
        if (externalDeviceId.Length is < 1 or > 100 ||
            !ExternalIdPattern.IsMatch(externalDeviceId))
        {
            error = "The topic contains an invalid external device identifier.";
            return false;
        }

        return true;
    }
}
