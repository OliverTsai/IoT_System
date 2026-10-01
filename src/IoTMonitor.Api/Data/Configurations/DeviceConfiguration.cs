using IoTMonitor.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTMonitor.Api.Data.Configurations;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(device => device.Id);

        builder.Property(device => device.Id)
            .HasColumnName("id");

        builder.Property(device => device.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(device => device.ExternalId)
            .IsUnique()
            .HasDatabaseName("ux_devices_external_id");

        builder.Property(device => device.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(device => device.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(device => device.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
