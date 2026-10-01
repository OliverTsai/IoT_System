using IoTMonitor.Api.Contracts.Common;
using IoTMonitor.Api.Contracts.Devices;
using IoTMonitor.Api.Contracts.Telemetry;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(
    IoTMonitorDbContext dbContext,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<DeviceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceResponse>> Create(
        CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var externalId = request.ExternalId!.Trim();

        if (await dbContext.Devices.AnyAsync(
                device => device.ExternalId == externalId,
                cancellationToken))
        {
            return DuplicateExternalId(externalId);
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            Name = request.Name!.Trim(),
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.Devices.Add(device);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsExternalIdConflict(exception))
        {
            return DuplicateExternalId(externalId);
        }

        var response = ToResponse(device);
        return CreatedAtAction(nameof(GetById), new { deviceId = device.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<DeviceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<DeviceResponse>>> GetAll(
        [FromQuery] DeviceListQuery query,
        CancellationToken cancellationToken)
    {
        var devices = dbContext.Devices.AsNoTracking();
        var totalCount = await devices.CountAsync(cancellationToken);
        var items = await devices
            .OrderByDescending(device => device.CreatedAtUtc)
            .ThenBy(device => device.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(device => new DeviceResponse(
                device.Id,
                device.ExternalId,
                device.Name,
                device.IsActive,
                device.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<DeviceResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            CalculateTotalPages(totalCount, query.PageSize)));
    }

    [HttpGet("{deviceId:guid}", Name = nameof(GetById))]
    [ProducesResponseType<DeviceDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDetailsResponse>> GetById(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

        if (device is null)
        {
            return DeviceNotFound(deviceId);
        }

        var latestTelemetry = await dbContext.Telemetry
            .AsNoTracking()
            .Where(telemetry => telemetry.DeviceId == deviceId)
            .OrderByDescending(telemetry => telemetry.RecordedAtUtc)
            .ThenByDescending(telemetry => telemetry.Id)
            .Select(telemetry => new TelemetryResponse(
                telemetry.Id,
                telemetry.DeviceId,
                telemetry.TemperatureCelsius,
                telemetry.HumidityPercent,
                telemetry.RecordedAtUtc,
                telemetry.ReceivedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new DeviceDetailsResponse(
            device.Id,
            device.ExternalId,
            device.Name,
            device.IsActive,
            device.CreatedAtUtc,
            latestTelemetry));
    }

    [HttpPatch("{deviceId:guid}/status")]
    [ProducesResponseType<DeviceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceResponse>> UpdateStatus(
        Guid deviceId,
        UpdateDeviceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .SingleOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

        if (device is null)
        {
            return DeviceNotFound(deviceId);
        }

        device.IsActive = request.IsActive!.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(device));
    }

    private ObjectResult DeviceNotFound(Guid deviceId)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Device not found",
            detail: $"Device '{deviceId}' does not exist.");
    }

    private ObjectResult DuplicateExternalId(string externalId)
    {
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Duplicate external identifier",
            detail: $"A device with external identifier '{externalId}' already exists.");
    }

    private static bool IsExternalIdConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_devices_external_id"
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static DeviceResponse ToResponse(Device device)
    {
        return new DeviceResponse(
            device.Id,
            device.ExternalId,
            device.Name,
            device.IsActive,
            device.CreatedAtUtc);
    }
}
