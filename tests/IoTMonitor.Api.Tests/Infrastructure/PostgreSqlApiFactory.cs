using IoTMonitor.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                    "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1"
            });
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
}
