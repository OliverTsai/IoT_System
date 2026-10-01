using IoTMonitor.Api.Contracts.Users;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = SecurityPolicies.AdminOnly)]
public sealed class UsersController(
    IoTMonitorDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username!.Trim();
        var normalizedUsername = UserCredentials.NormalizeUsername(username);

        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            return DuplicateUsername();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = string.Empty,
            Role = request.Role!.Value,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUsernameConflict(exception))
        {
            return DuplicateUsername();
        }

        return CreatedAtAction(nameof(GetAll), ToResponse(user));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Username)
            .Select(user => new UserResponse(
                user.Id,
                user.Username,
                user.Role,
                user.IsActive,
                user.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    private ObjectResult DuplicateUsername()
    {
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Duplicate username",
            detail: "A user with that username already exists.");
    }

    private static bool IsUsernameConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_normalized_username"
        };
    }

    private static UserResponse ToResponse(ApplicationUser user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.Role,
            user.IsActive,
            user.CreatedAtUtc);
    }
}
