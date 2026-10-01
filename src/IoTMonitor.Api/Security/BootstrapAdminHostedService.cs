using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IoTMonitor.Api.Security;

public sealed class BootstrapAdminHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ApplicationAuthenticationOptions> authenticationOptions,
    TimeProvider timeProvider,
    ILogger<BootstrapAdminHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrap = authenticationOptions.Value.BootstrapAdmin;
        if (!bootstrap.Enabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IoTMonitorDbContext>();
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var normalizedUsername = UserCredentials.NormalizeUsername(bootstrap.Username);

        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            logger.LogInformation(
                "Bootstrap administrator was not created because the configured user already exists.");
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Username = bootstrap.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            PasswordHash = string.Empty,
            Role = UserRole.Admin,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = passwordHasher.HashPassword(user, bootstrap.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bootstrap administrator {UserId} was created. Disable bootstrap configuration and remove its password secret.",
            user.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
