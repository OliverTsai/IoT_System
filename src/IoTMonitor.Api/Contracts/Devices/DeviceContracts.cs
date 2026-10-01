using System.ComponentModel.DataAnnotations;
using IoTMonitor.Api.Contracts.Telemetry;

namespace IoTMonitor.Api.Contracts.Devices;

public sealed record CreateDeviceRequest(
    [Required]
    [StringLength(100, MinimumLength = 1)]
    [RegularExpression(
        @"^[A-Za-z0-9][A-Za-z0-9._:-]*$",
        ErrorMessage = "ExternalId may contain only letters, numbers, '.', '_', ':', and '-'.")]
    string? ExternalId,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    [RegularExpression(
        @"^(?=.*\S)[^\r\n]+$",
        ErrorMessage = "Name must contain a non-whitespace character and cannot contain a line break.")]
    string? Name);

public sealed record UpdateDeviceStatusRequest(
    [Required] bool? IsActive);

public sealed record DeviceResponse(
    Guid Id,
    string ExternalId,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record DeviceDetailsResponse(
    Guid Id,
    string ExternalId,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc,
    TelemetryResponse? LatestTelemetry);

public sealed class DeviceListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
