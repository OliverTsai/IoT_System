using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTMonitor.Api.Contracts.Auth;
using IoTMonitor.Api.Contracts.Devices;
using IoTMonitor.Api.Contracts.Users;
using IoTMonitor.Api.Data;
using IoTMonitor.Api.Domain.Entities;
using IoTMonitor.Api.Domain.Enums;
using IoTMonitor.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IoTMonitor.Api.Tests;

public sealed class AuthenticationAuthorizationTests(PostgreSqlApiFactory factory)
    : IClassFixture<PostgreSqlApiFactory>
{
    private const string DatabaseSkipReason =
        "Set IOT_MONITOR_TEST_CONNECTION_STRING to run PostgreSQL integration tests.";

    public static bool IsDatabaseConfigured => PostgreSqlApiFactory.IsConfigured;

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task DeviceRead_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(
            "/api/devices",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task Login_WithInvalidPassword_ReturnsGenericUnauthorizedResponse()
    {
        using var client = factory.CreateHttpsClient();
        var response = await client.LoginAsync(
            PostgreSqlApiFactory.AdminUsername,
            "Incorrect-Password-123!",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Invalid credentials", problem.Title);
        Assert.DoesNotContain(PostgreSqlApiFactory.AdminUsername, problem.Detail);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task Viewer_CanReadDevicesButCannotCreateOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Viewer,
            cancellationToken);

        var readResponse = await client.GetAsync("/api/devices", cancellationToken);
        var writeResponse = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest($"viewer-{Guid.NewGuid():N}", "Forbidden viewer device"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task Admin_CanCreateDevice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest($"admin-{Guid.NewGuid():N}", "Authorized admin device"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task StateChangingRequest_WithoutCsrfToken_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        client.DefaultRequestHeaders.Remove(HttpClientSecurityExtensions.CsrfHeaderName);

        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest($"csrf-{Guid.NewGuid():N}", "Missing CSRF token"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task AuthenticationCookie_AfterExpiration_ReturnsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var beforeExpiration = await client.GetAsync("/api/auth/me", cancellationToken);

        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        var afterExpiration = await client.GetAsync("/api/auth/me", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, beforeExpiration.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterExpiration.StatusCode);
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task Login_SetsSecureHttpOnlyCookieAndSecurityHeaders()
    {
        using var client = factory.CreateHttpsClient();
        var response = await client.LoginAsync(
            PostgreSqlApiFactory.AdminUsername,
            PostgreSqlApiFactory.AdminPassword,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var setCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-IoTMonitor.Auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact(Skip = DatabaseSkipReason, SkipUnless = nameof(IsDatabaseConfigured))]
    public async Task Admin_CreatesUserWithHashedPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateAuthenticatedClientAsync(
            UserRole.Admin,
            cancellationToken);
        var username = $"new-viewer-{Guid.NewGuid():N}";
        const string password = "Viewer-New-Password-123!";

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(username, password, UserRole.Viewer),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>(
            ResponseJsonOptions,
            cancellationToken);
        Assert.NotNull(created);
        Assert.Equal(UserRole.Viewer, created.Role);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IoTMonitorDbContext>();
        var stored = await dbContext.Users.SingleAsync(
            user => user.NormalizedUsername == username.ToUpperInvariant(),
            cancellationToken);
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();

        Assert.NotEqual(password, stored.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(stored, stored.PasswordHash, password));
    }
}
