using IoTMonitor.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTMonitor.Api.Data.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(alert => alert.Id);

        builder.Property(alert => alert.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(alert => alert.DeviceId)
            .HasColumnName("device_id");

        builder.Property(alert => alert.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(alert => alert.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(alert => alert.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(alert => alert.OccurredAtUtc)
            .HasColumnName("occurred_at_utc");

        builder.Property(alert => alert.AcknowledgedAtUtc)
            .HasColumnName("acknowledged_at_utc");

        builder.HasIndex(alert => new { alert.DeviceId, alert.OccurredAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_alerts_device_occurred_at");

        builder.HasOne(alert => alert.Device)
            .WithMany(device => device.Alerts)
            .HasForeignKey(alert => alert.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
