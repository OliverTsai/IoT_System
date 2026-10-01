using System.Globalization;
using IoTMonitor.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Alerts;

public sealed record AlertCandidate(
    AlertType Type,
    AlertSeverity Severity,
    string Message);

public sealed class AlertRuleEvaluator(IOptions<AlertRulesOptions> options)
{
    private readonly AlertRulesOptions _options = options.Value;

    public IReadOnlyList<AlertCandidate> Evaluate(
        decimal temperatureCelsius,
        decimal humidityPercent)
    {
        if (!_options.Enabled)
        {
            return [];
        }

        var alerts = new List<AlertCandidate>(2);
        var temperatureAlert = EvaluateTemperature(temperatureCelsius);
        if (temperatureAlert is not null)
        {
            alerts.Add(temperatureAlert);
        }

        var humidityAlert = EvaluateHumidity(humidityPercent);
        if (humidityAlert is not null)
        {
            alerts.Add(humidityAlert);
        }

        return alerts;
    }

    private AlertCandidate? EvaluateTemperature(decimal value)
    {
        if (value < _options.TemperatureCriticalMinimumCelsius)
        {
            return CreateCandidate(
                AlertType.TemperatureOutOfRange,
                AlertSeverity.Critical,
                "Temperature",
                value,
                "°C",
                "below",
                _options.TemperatureCriticalMinimumCelsius,
                "critical minimum");
        }

        if (value > _options.TemperatureCriticalMaximumCelsius)
        {
            return CreateCandidate(
                AlertType.TemperatureOutOfRange,
                AlertSeverity.Critical,
                "Temperature",
                value,
                "°C",
                "above",
                _options.TemperatureCriticalMaximumCelsius,
                "critical maximum");
        }

        if (value < _options.TemperatureMinimumCelsius)
        {
            return CreateCandidate(
                AlertType.TemperatureOutOfRange,
                AlertSeverity.Warning,
                "Temperature",
                value,
                "°C",
                "below",
                _options.TemperatureMinimumCelsius,
                "warning minimum");
        }

        if (value > _options.TemperatureMaximumCelsius)
        {
            return CreateCandidate(
                AlertType.TemperatureOutOfRange,
                AlertSeverity.Warning,
                "Temperature",
                value,
                "°C",
                "above",
                _options.TemperatureMaximumCelsius,
                "warning maximum");
        }

        return null;
    }

    private AlertCandidate? EvaluateHumidity(decimal value)
    {
        if (value < _options.HumidityCriticalMinimumPercent)
        {
            return CreateCandidate(
                AlertType.HumidityOutOfRange,
                AlertSeverity.Critical,
                "Humidity",
                value,
                "%",
                "below",
                _options.HumidityCriticalMinimumPercent,
                "critical minimum");
        }

        if (value > _options.HumidityCriticalMaximumPercent)
        {
            return CreateCandidate(
                AlertType.HumidityOutOfRange,
                AlertSeverity.Critical,
                "Humidity",
                value,
                "%",
                "above",
                _options.HumidityCriticalMaximumPercent,
                "critical maximum");
        }

        if (value < _options.HumidityMinimumPercent)
        {
            return CreateCandidate(
                AlertType.HumidityOutOfRange,
                AlertSeverity.Warning,
                "Humidity",
                value,
                "%",
                "below",
                _options.HumidityMinimumPercent,
                "warning minimum");
        }

        if (value > _options.HumidityMaximumPercent)
        {
            return CreateCandidate(
                AlertType.HumidityOutOfRange,
                AlertSeverity.Warning,
                "Humidity",
                value,
                "%",
                "above",
                _options.HumidityMaximumPercent,
                "warning maximum");
        }

        return null;
    }

    private static AlertCandidate CreateCandidate(
        AlertType type,
        AlertSeverity severity,
        string metric,
        decimal value,
        string unit,
        string direction,
        decimal threshold,
        string thresholdName)
    {
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1:0.0} {2} is {3} the {4} of {5:0.0} {2}.",
            metric,
            value,
            unit,
            direction,
            thresholdName,
            threshold);

        return new AlertCandidate(type, severity, message);
    }
}
