namespace IoTMonitor.Api.Alerts;

public sealed class AlertRulesOptions
{
    public const string SectionName = "AlertRules";

    public bool Enabled { get; init; } = true;

    public decimal TemperatureMinimumCelsius { get; init; } = 10m;

    public decimal TemperatureMaximumCelsius { get; init; } = 35m;

    public decimal TemperatureCriticalMinimumCelsius { get; init; } = 0m;

    public decimal TemperatureCriticalMaximumCelsius { get; init; } = 45m;

    public decimal HumidityMinimumPercent { get; init; } = 30m;

    public decimal HumidityMaximumPercent { get; init; } = 75m;

    public decimal HumidityCriticalMinimumPercent { get; init; } = 15m;

    public decimal HumidityCriticalMaximumPercent { get; init; } = 90m;

    public bool HasValidThresholdOrder()
    {
        return TemperatureCriticalMinimumCelsius <= TemperatureMinimumCelsius &&
            TemperatureMinimumCelsius < TemperatureMaximumCelsius &&
            TemperatureMaximumCelsius <= TemperatureCriticalMaximumCelsius &&
            HumidityCriticalMinimumPercent <= HumidityMinimumPercent &&
            HumidityMinimumPercent < HumidityMaximumPercent &&
            HumidityMaximumPercent <= HumidityCriticalMaximumPercent;
    }

    public bool HasSupportedValues()
    {
        return TemperatureCriticalMinimumCelsius >= -100m &&
            TemperatureCriticalMaximumCelsius <= 200m &&
            HumidityCriticalMinimumPercent >= 0m &&
            HumidityCriticalMaximumPercent <= 100m;
    }
}
