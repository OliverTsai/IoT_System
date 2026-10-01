using IoTMonitor.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitor.Api.Data;

public sealed class IoTMonitorDbContext(DbContextOptions<IoTMonitorDbContext> options)
    : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Telemetry> Telemetry => Set<Telemetry>();

    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IoTMonitorDbContext).Assembly);
    }
}
