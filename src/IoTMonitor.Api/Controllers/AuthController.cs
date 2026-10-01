using System.Security.Claims;
using IoTMonitor.Api.Contracts.Auth;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IoTMonitorDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IAntiforgery antiforgery,
    IOptions<ApplicationAuthenticationOptions> authenticationOptions,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        Response.Headers.CacheControl = "no-store";
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.LoginRateLimit)]
    [ProducesResponseType<AuthenticatedUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticatedUserResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var normalizedUsername = UserCredentials.NormalizeUsername(request.Username!);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password!);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(authenticationOptions.Value.CookieLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(SecurityClaimTypes.SecurityStamp, user.SecurityStamp)
        };
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            AllowRefresh = false,
            ExpiresUtc = expiresAtUtc,
            IsPersistent = false,
            IssuedUtc = now
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        return Ok(ToResponse(user, expiresAtUtc));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        Response.Headers.CacheControl = "no-store";
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<AuthenticatedUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserResponse>> GetCurrentUser()
    {
        Response.Headers.CacheControl = "no-store";
        var authentication = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name);
        var roleValue = User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(idValue, out var userId) ||
            string.IsNullOrEmpty(username) ||
            !Enum.TryParse<UserRole>(roleValue, out var role) ||
            authentication.Properties?.ExpiresUtc is not { } expiresAtUtc)
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedUserResponse(userId, username, role, expiresAtUtc));
    }

    private ObjectResult InvalidCredentials()
    {
        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials",
            detail: "The username or password is invalid.");
    }

    private static AuthenticatedUserResponse ToResponse(
        ApplicationUser user,
        DateTimeOffset expiresAtUtc)
    {
        return new AuthenticatedUserResponse(
            user.Id,
            user.Username,
            user.Role,
            expiresAtUtc);
    }
}
