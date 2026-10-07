using IoTMonitor.Api.Devices;

namespace IoTMonitor.Api.Tests;

public sealed class DevicePresenceTests
{
    private static readonly DateTime NowUtc = new(
        2026,
        10,
        7,
        8,
        0,
        30,
        DateTimeKind.Utc);

    [Fact]
    public void IsOnline_WithRecentTelemetry_ReturnsTrue()
    {
        var result = DevicePresence.IsOnline(
            true,
            NowUtc.Subtract(TimeSpan.FromSeconds(29)),
            NowUtc);

        Assert.True(result);
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -31)]
    public void IsOnline_WithDisabledOrStaleDevice_ReturnsFalse(
        bool isActive,
        int lastSeenOffsetSeconds)
    {
        var result = DevicePresence.IsOnline(
            isActive,
            NowUtc.AddSeconds(lastSeenOffsetSeconds),
            NowUtc);

        Assert.False(result);
    }

    [Fact]
    public void IsOnline_WithoutTelemetry_ReturnsFalse()
    {
        Assert.False(DevicePresence.IsOnline(true, null, NowUtc));
    }
}
