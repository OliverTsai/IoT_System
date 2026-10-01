using System.ComponentModel.DataAnnotations;

namespace IoTMonitor.Api.Contracts.Telemetry;

public sealed record CreateTelemetryRequest(
    [Required]
    [Range(typeof(decimal), "-100", "200")]
    decimal? TemperatureCelsius,
    [Required]
    [Range(typeof(decimal), "0", "100")]
    decimal? HumidityPercent,
    [Required]
    DateTimeOffset? RecordedAtUtc);

public sealed record TelemetryResponse(
    long Id,
    Guid DeviceId,
    decimal TemperatureCelsius,
    decimal HumidityPercent,
    DateTime RecordedAtUtc,
    DateTime ReceivedAtUtc);

public sealed class TelemetryListQuery
{
    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
