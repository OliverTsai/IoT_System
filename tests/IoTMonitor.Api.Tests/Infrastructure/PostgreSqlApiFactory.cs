using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.Security;
using IoTMonitor.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace IoTMonitor.Api.Tests.Infrastructure;

public sealed class PostgreSqlApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ConnectionStringEnvironmentVariable =
        "IOT_MONITOR_TEST_CONNECTION_STRING";

    private readonly string? _adminConnectionString;
    private readonly string? _testConnectionString;
    private readonly string _databaseName = $"iot_monitor_tests_{Guid.NewGuid():N}";
    private bool _databaseCreated;

    public const string AdminUsername = "integration-admin";
    public const string AdminPassword = "Admin-Test-Password-123!";
    public const string OperatorUsername = "integration-operator";
    public const string OperatorPassword = "Operator-Test-Password-123!";
    public const string ViewerUsername = "integration-viewer";
    public const string ViewerPassword = "Viewer-Test-Password-123!";

    public AdjustableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public PostgreSqlApiFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        _adminConnectionString = adminBuilder.ConnectionString;

        var testBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = _databaseName
        };
        _testConnectionString = testBuilder.ConnectionString;
    }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IoTMonitor"] = _testConnectionString ??
                    "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1",
                ["Security:GlobalPermitLimit"] = "10000",
                ["Security:LoginPermitLimit"] = "100"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async ValueTask InitializeAsync()
    {
        if (_adminConnectionString is null || _testConnectionString is null)
        {
            return;
        }

        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            await command.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IoTMonitorDbContext>();
        await dbContext.Database.MigrateAsync();
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();
        await AddUserAsync(
            dbContext,
            passwordHasher,
            AdminUsername,
            AdminPassword,
            UserRole.Admin);
        await AddUserAsync(
            dbContext,
            passwordHasher,
            OperatorUsername,
            OperatorPassword,
            UserRole.Operator);
        await AddUserAsync(
            dbContext,
            passwordHasher,
            ViewerUsername,
            ViewerPassword,
            UserRole.Viewer);
        await dbContext.SaveChangesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (!_databaseCreated || _adminConnectionString is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        UserRole role,
        CancellationToken cancellationToken)
    {
        var client = CreateHttpsClient();
        var (username, password) = role switch
        {
            UserRole.Admin => (AdminUsername, AdminPassword),
            UserRole.Operator => (OperatorUsername, OperatorPassword),
            UserRole.Viewer => (ViewerUsername, ViewerPassword),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };

        var response = await client.LoginAsync(
            username,
            password,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private async Task AddUserAsync(
        IoTMonitorDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        string username,
        string password,
        UserRole role)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = UserCredentials.NormalizeUsername(username),
            PasswordHash = string.Empty,
            Role = role,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = Clock.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        await dbContext.Users.AddAsync(user);
    }
}
