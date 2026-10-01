using System.ComponentModel.DataAnnotations;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.Security;

namespace IoTMonitor.Api.Contracts.Users;

public sealed record CreateUserRequest(
    [Required]
    [StringLength(64, MinimumLength = 3)]
    [RegularExpression(
        @"^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$",
        ErrorMessage = "Username may contain only letters, numbers, '.', '_', and '-'.")]
    string? Username,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string? Password,
    [Required]
    UserRole? Role) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role.HasValue && !Enum.IsDefined(Role.Value))
        {
            yield return new ValidationResult(
                "Role must be Viewer, Operator, or Admin.",
                [nameof(Role)]);
        }

        if (Password is not null && !UserCredentials.IsValidPassword(Password))
        {
            yield return new ValidationResult(
                "Password must be 12-128 characters and include uppercase, lowercase, number, and symbol.",
                [nameof(Password)]);
        }
    }
}

public sealed record UserResponse(
    Guid Id,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAtUtc);
