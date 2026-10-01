using System.ComponentModel.DataAnnotations;
using IoTMonitor.Api.Domain.Enums;

namespace IoTMonitor.Api.Contracts.Auth;

public sealed record LoginRequest(
    [Required]
    [StringLength(64, MinimumLength = 3)]
    string? Username,
    [Required]
    [StringLength(128, MinimumLength = 1)]
    string? Password);

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Username,
    UserRole Role,
    DateTimeOffset ExpiresAtUtc);

public sealed record CsrfTokenResponse(string Token);
