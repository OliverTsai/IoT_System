using System.Security.Claims;
using IoTMonitor.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace IoTMonitor.Api.Security;

public sealed class ApplicationCookieEvents(IoTMonitorDbContext dbContext)
    : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(
        RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(
        RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var securityStamp = context.Principal?.FindFirstValue(SecurityClaimTypes.SecurityStamp);

        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrEmpty(securityStamp))
        {
            await RejectPrincipalAsync(context);
            return;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.SecurityStamp,
                candidate.Role
            })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        var claimedRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
        if (user is null ||
            !user.IsActive ||
            !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal) ||
            !string.Equals(user.Role.ToString(), claimedRole, StringComparison.Ordinal))
        {
            await RejectPrincipalAsync(context);
        }
    }

    private static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
