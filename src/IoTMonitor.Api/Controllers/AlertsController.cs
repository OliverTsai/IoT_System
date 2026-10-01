using IoTMonitor.Api.Contracts.Alerts;
using IoTMonitor.Api.Contracts.Common;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Realtime;
using IoTMonitor.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(
    IoTMonitorDbContext dbContext,
    MonitoringEventPublisher eventPublisher,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AlertResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AlertResponse>>> GetAll(
        [FromQuery] AlertListQuery query,
        CancellationToken cancellationToken)
    {
        var alerts = dbContext.Alerts.AsNoTracking().AsQueryable();

        if (query.DeviceId.HasValue)
        {
            alerts = alerts.Where(alert => alert.DeviceId == query.DeviceId.Value);
        }

        if (query.Type.HasValue)
        {
            alerts = alerts.Where(alert => alert.Type == query.Type.Value);
        }

        if (query.Severity.HasValue)
        {
            alerts = alerts.Where(alert => alert.Severity == query.Severity.Value);
        }

        if (query.Acknowledged.HasValue)
        {
            alerts = query.Acknowledged.Value
                ? alerts.Where(alert => alert.AcknowledgedAtUtc != null)
                : alerts.Where(alert => alert.AcknowledgedAtUtc == null);
        }

        var totalCount = await alerts.CountAsync(cancellationToken);
        var items = await alerts
            .OrderByDescending(alert => alert.OccurredAtUtc)
            .ThenByDescending(alert => alert.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(alert => new AlertResponse(
                alert.Id,
                alert.DeviceId,
                alert.Device.ExternalId,
                alert.Device.Name,
                alert.Type,
                alert.Severity,
                alert.Message,
                alert.OccurredAtUtc,
                alert.AcknowledgedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<AlertResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            CalculateTotalPages(totalCount, query.PageSize)));
    }

    [HttpPatch("{alertId:long}/acknowledge")]
    [Authorize(Policy = SecurityPolicies.OperatorOrAdmin)]
    [ProducesResponseType<AlertResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertResponse>> Acknowledge(
        long alertId,
        CancellationToken cancellationToken)
    {
        var alert = await dbContext.Alerts
            .Include(candidate => candidate.Device)
            .SingleOrDefaultAsync(candidate => candidate.Id == alertId, cancellationToken);

        if (alert is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Alert not found",
                detail: $"Alert '{alertId}' does not exist.");
        }

        var changed = alert.AcknowledgedAtUtc is null;
        if (changed)
        {
            var acknowledgedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            alert.AcknowledgedAtUtc = acknowledgedAtUtc.AddTicks(
                -(acknowledgedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = ToResponse(alert);
        if (changed)
        {
            await eventPublisher.PublishAlertAcknowledgedAsync(response);
        }

        return Ok(response);
    }

    private static AlertResponse ToResponse(Alert alert)
    {
        return new AlertResponse(
            alert.Id,
            alert.DeviceId,
            alert.Device.ExternalId,
            alert.Device.Name,
            alert.Type,
            alert.Severity,
            alert.Message,
            alert.OccurredAtUtc,
            alert.AcknowledgedAtUtc);
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
