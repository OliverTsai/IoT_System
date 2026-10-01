using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Messaging.Mqtt;

public interface ITelemetryDeduplicator
{
    bool TryReserve(string externalDeviceId, Guid messageId);

    void Release(string externalDeviceId, Guid messageId);
}

public sealed class TelemetryDeduplicator(
    IOptions<MqttOptions> options,
    TimeProvider timeProvider) : ITelemetryDeduplicator
{
    private readonly object _gate = new();
    private readonly Dictionary<MessageKey, DateTimeOffset> _reservations = [];
    private readonly TimeSpan _window = TimeSpan.FromMinutes(options.Value.DuplicateWindowMinutes);
    private readonly int _maximumEntries = options.Value.MaxTrackedMessageIds;

    public bool TryReserve(string externalDeviceId, Guid messageId)
    {
        var key = new MessageKey(externalDeviceId, messageId);
        var now = timeProvider.GetUtcNow();

        lock (_gate)
        {
            RemoveExpired(now);

            if (_reservations.ContainsKey(key))
            {
                return false;
            }

            if (_reservations.Count >= _maximumEntries)
            {
                var oldest = _reservations.MinBy(reservation => reservation.Value).Key;
                _reservations.Remove(oldest);
            }

            _reservations.Add(key, now.Add(_window));
            return true;
        }
    }

    public void Release(string externalDeviceId, Guid messageId)
    {
        lock (_gate)
        {
            _reservations.Remove(new MessageKey(externalDeviceId, messageId));
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _reservations
                     .Where(reservation => reservation.Value <= now)
                     .Select(reservation => reservation.Key)
                     .ToArray())
        {
            _reservations.Remove(key);
        }
    }

    private readonly record struct MessageKey(string ExternalDeviceId, Guid MessageId);
}
