using IoTMonitor.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTMonitor.Api.Data.Configurations;

public sealed class TelemetryConfiguration : IEntityTypeConfiguration<Telemetry>
{
    public void Configure(EntityTypeBuilder<Telemetry> builder)
    {
        builder.ToTable(
            "telemetry",
            table => table.HasCheckConstraint(
                "ck_telemetry_humidity_percent",
                "humidity_percent >= 0 AND humidity_percent <= 100"));

        builder.HasKey(telemetry => telemetry.Id);

        builder.Property(telemetry => telemetry.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(telemetry => telemetry.DeviceId)
            .HasColumnName("device_id");

        builder.Property(telemetry => telemetry.TemperatureCelsius)
            .HasColumnName("temperature_celsius")
            .HasPrecision(5, 2);

        builder.Property(telemetry => telemetry.HumidityPercent)
            .HasColumnName("humidity_percent")
            .HasPrecision(5, 2);

        builder.Property(telemetry => telemetry.RecordedAtUtc)
            .HasColumnName("recorded_at_utc");

        builder.Property(telemetry => telemetry.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(telemetry => new { telemetry.DeviceId, telemetry.RecordedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_telemetry_device_recorded_at");

        builder.HasOne(telemetry => telemetry.Device)
            .WithMany(device => device.TelemetryReadings)
            .HasForeignKey(telemetry => telemetry.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
