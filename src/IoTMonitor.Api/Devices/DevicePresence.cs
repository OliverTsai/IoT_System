namespace IoTMonitor.Api.Devices;

public static class DevicePresence
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(30);

    public static bool IsOnline(
        bool isActive,
        DateTime? lastSeenAtUtc,
        DateTime nowUtc)
    {
        return isActive &&
            lastSeenAtUtc.HasValue &&
            lastSeenAtUtc.Value >= nowUtc.Subtract(OnlineWindow);
    }
}
