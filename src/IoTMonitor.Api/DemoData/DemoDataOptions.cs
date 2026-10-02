using System.Text.RegularExpressions;

namespace IoTMonitor.Api.DemoData;

public sealed partial class DemoDataOptions
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; init; }

    public string DevicePrefix { get; init; } = "sim-device";

    public int DeviceCount { get; init; } = 3;

    public bool HasValidConfiguration()
    {
        return !Enabled ||
            (DeviceCount is >= 1 and <= 100 &&
             !string.IsNullOrWhiteSpace(DevicePrefix) &&
             DevicePrefix.Length <= 95 &&
             DevicePrefixPattern().IsMatch(DevicePrefix));
    }

    public string GetExternalId(int oneBasedIndex)
    {
        return $"{DevicePrefix}-{oneBasedIndex:000}";
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DevicePrefixPattern();
}
