using IoTMonitor.Api.DemoData;

namespace IoTMonitor.Api.Tests;

public sealed class DemoDataOptionsTests
{
    [Fact]
    public void HasValidConfiguration_WithSupportedValues_ReturnsTrue()
    {
        var options = new DemoDataOptions
        {
            Enabled = true,
            DevicePrefix = "sim-device",
            DeviceCount = 3
        };

        Assert.True(options.HasValidConfiguration());
        Assert.Equal("sim-device-003", options.GetExternalId(3));
    }

    [Theory]
    [InlineData("", 3)]
    [InlineData("invalid prefix", 3)]
    [InlineData("sim-device", 0)]
    [InlineData("sim-device", 101)]
    public void HasValidConfiguration_WithUnsupportedValues_ReturnsFalse(
        string prefix,
        int count)
    {
        var options = new DemoDataOptions
        {
            Enabled = true,
            DevicePrefix = prefix,
            DeviceCount = count
        };

        Assert.False(options.HasValidConfiguration());
    }

    [Fact]
    public void HasValidConfiguration_WhenDisabled_IgnoresSeedValues()
    {
        var options = new DemoDataOptions
        {
            Enabled = false,
            DevicePrefix = "",
            DeviceCount = 0
        };

        Assert.True(options.HasValidConfiguration());
    }
}
