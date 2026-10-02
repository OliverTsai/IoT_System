using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.DemoData;

public sealed class DemoDeviceHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DemoDataOptions> options,
    TimeProvider timeProvider,
    ILogger<DemoDeviceHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IoTMonitorDbContext>();
        var expectedDevices = Enumerable
            .Range(1, configuration.DeviceCount)
            .Select(index => new
            {
                ExternalId = configuration.GetExternalId(index),
                Index = index
            })
            .ToArray();
        var externalIds = expectedDevices.Select(device => device.ExternalId).ToArray();
        var existingExternalIds = await dbContext.Devices
            .Where(device => externalIds.Contains(device.ExternalId))
            .Select(device => device.ExternalId)
            .ToHashSetAsync(cancellationToken);
        var createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var newDevices = expectedDevices
            .Where(device => !existingExternalIds.Contains(device.ExternalId))
            .Select(device => new Device
            {
                Id = Guid.NewGuid(),
                ExternalId = device.ExternalId,
                Name = $"Simulated device {device.Index}",
                IsActive = true,
                CreatedAtUtc = createdAtUtc
            })
            .ToArray();

        if (newDevices.Length == 0)
        {
            logger.LogInformation("Demo devices already exist; no seed data was added.");
            return;
        }

        dbContext.Devices.AddRange(newDevices);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created {DeviceCount} demo devices.", newDevices.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
