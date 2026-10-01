using IoTMonitor.Api.Alerts;
using IoTMonitor.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Tests;

public sealed class AlertRuleEvaluatorTests
{
    [Theory]
    [InlineData(10, 30)]
    [InlineData(35, 75)]
    [InlineData(22.5, 52.5)]
    public void Evaluate_WithValuesInsideWarningBoundaries_ReturnsNoAlerts(
        decimal temperature,
        decimal humidity)
    {
        var evaluator = CreateEvaluator();

        var alerts = evaluator.Evaluate(temperature, humidity);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_WithValuesOutsideWarningBoundaries_ReturnsWarningAlerts()
    {
        var evaluator = CreateEvaluator();

        var alerts = evaluator.Evaluate(9.9m, 75.1m);

        Assert.Collection(
            alerts,
            temperature =>
            {
                Assert.Equal(AlertType.TemperatureOutOfRange, temperature.Type);
                Assert.Equal(AlertSeverity.Warning, temperature.Severity);
                Assert.Contains("9.9", temperature.Message, StringComparison.Ordinal);
            },
            humidity =>
            {
                Assert.Equal(AlertType.HumidityOutOfRange, humidity.Type);
                Assert.Equal(AlertSeverity.Warning, humidity.Severity);
                Assert.Contains("75.1", humidity.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Evaluate_WithValuesOutsideCriticalBoundaries_ReturnsCriticalAlerts()
    {
        var evaluator = CreateEvaluator();

        var alerts = evaluator.Evaluate(-0.1m, 90.1m);

        Assert.All(alerts, alert => Assert.Equal(AlertSeverity.Critical, alert.Severity));
        Assert.Equal(2, alerts.Count);
    }

    [Fact]
    public void Evaluate_WhenRulesAreDisabled_ReturnsNoAlerts()
    {
        var evaluator = CreateEvaluator(new AlertRulesOptions { Enabled = false });

        var alerts = evaluator.Evaluate(-100m, 100m);

        Assert.Empty(alerts);
    }

    private static AlertRuleEvaluator CreateEvaluator(AlertRulesOptions? options = null)
    {
        return new AlertRuleEvaluator(Options.Create(options ?? new AlertRulesOptions()));
    }
}
