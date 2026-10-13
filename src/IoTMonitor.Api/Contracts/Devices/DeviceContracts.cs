using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    string? Name,
    [StringLength(50, MinimumLength = 1)]
    [RegularExpression(
        @"^[a-z0-9][a-z0-9-]*$",
        ErrorMessage = "ProtocolKey must be a lowercase kebab-case identifier.")]
    string? ProtocolKey = null,
    JsonElement? ProtocolSettings = null);

public sealed record UpdateDeviceStatusRequest(
    [Required] bool? IsActive);

public sealed record DeviceResponse(
    Guid Id,
    string ExternalId,
    string Name,
    string ProtocolKey,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastSeenAtUtc,
    bool IsOnline);

public sealed record DeviceDetailsResponse(
    Guid Id,
    string ExternalId,
    string Name,
    string ProtocolKey,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastSeenAtUtc,
    bool IsOnline,
    TelemetryResponse? LatestTelemetry);

public sealed class DeviceListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
