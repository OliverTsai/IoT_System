using IoTMonitor.Api.Contracts.Common;
using IoTMonitor.Api.Contracts.Telemetry;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:guid}/telemetry")]
public sealed class DeviceTelemetryController(
    IoTMonitorDbContext dbContext,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.OperatorOrAdmin)]
    [ProducesResponseType<TelemetryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryResponse>> Create(
        Guid deviceId,
        CreateTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        var recordedAtUtc = request.RecordedAtUtc!.Value.ToUniversalTime();
        var now = timeProvider.GetUtcNow();

        if (recordedAtUtc > now.AddMinutes(5))
        {
            ModelState.AddModelError(
                nameof(request.RecordedAtUtc),
                "RecordedAtUtc cannot be more than five minutes in the future.");
            return ValidationProblem(ModelState);
        }

        if (!await dbContext.Devices.AnyAsync(
                device => device.Id == deviceId,
                cancellationToken))
        {
            return DeviceNotFound(deviceId);
        }

        var telemetry = new Telemetry
        {
            DeviceId = deviceId,
            TemperatureCelsius = request.TemperatureCelsius!.Value,
            HumidityPercent = request.HumidityPercent!.Value,
            RecordedAtUtc = recordedAtUtc.UtcDateTime,
            ReceivedAtUtc = now.UtcDateTime
        };

        dbContext.Telemetry.Add(telemetry);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(telemetry);
        return CreatedAtAction(nameof(GetLatest), new { deviceId }, response);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<TelemetryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<TelemetryResponse>>> GetHistory(
        Guid deviceId,
        [FromQuery] TelemetryListQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            ModelState.AddModelError(
                nameof(query.FromUtc),
                "FromUtc must be earlier than or equal to ToUtc.");
            return ValidationProblem(ModelState);
        }

        if (!await dbContext.Devices.AsNoTracking().AnyAsync(
                device => device.Id == deviceId,
                cancellationToken))
        {
            return DeviceNotFound(deviceId);
        }

        var telemetryQuery = dbContext.Telemetry
            .AsNoTracking()
            .Where(telemetry => telemetry.DeviceId == deviceId);

        if (query.FromUtc.HasValue)
        {
            var fromUtc = query.FromUtc.Value.UtcDateTime;
            telemetryQuery = telemetryQuery.Where(telemetry => telemetry.RecordedAtUtc >= fromUtc);
        }

        if (query.ToUtc.HasValue)
        {
            var toUtc = query.ToUtc.Value.UtcDateTime;
            telemetryQuery = telemetryQuery.Where(telemetry => telemetry.RecordedAtUtc <= toUtc);
        }

        var totalCount = await telemetryQuery.CountAsync(cancellationToken);
        var items = await telemetryQuery
            .OrderByDescending(telemetry => telemetry.RecordedAtUtc)
            .ThenByDescending(telemetry => telemetry.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(telemetry => new TelemetryResponse(
                telemetry.Id,
                telemetry.DeviceId,
                telemetry.TemperatureCelsius,
                telemetry.HumidityPercent,
                telemetry.RecordedAtUtc,
                telemetry.ReceivedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<TelemetryResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            CalculateTotalPages(totalCount, query.PageSize)));
    }

    [HttpGet("latest", Name = nameof(GetLatest))]
    [ProducesResponseType<TelemetryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryResponse>> GetLatest(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Devices.AsNoTracking().AnyAsync(
                device => device.Id == deviceId,
                cancellationToken))
        {
            return DeviceNotFound(deviceId);
        }

        var latest = await dbContext.Telemetry
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

        if (latest is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Telemetry not found",
                detail: $"Device '{deviceId}' does not have any telemetry readings.");
        }

        return Ok(latest);
    }

    private ObjectResult DeviceNotFound(Guid deviceId)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Device not found",
            detail: $"Device '{deviceId}' does not exist.");
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static TelemetryResponse ToResponse(Telemetry telemetry)
    {
        return new TelemetryResponse(
            telemetry.Id,
            telemetry.DeviceId,
            telemetry.TemperatureCelsius,
            telemetry.HumidityPercent,
            telemetry.RecordedAtUtc,
            telemetry.ReceivedAtUtc);
    }
}
