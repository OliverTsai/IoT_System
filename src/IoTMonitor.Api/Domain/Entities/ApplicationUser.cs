using IoTMonitor.Api.Domain.Enums;

namespace IoTMonitor.Api.Domain.Entities;

public sealed class ApplicationUser
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public required string SecurityStamp { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
