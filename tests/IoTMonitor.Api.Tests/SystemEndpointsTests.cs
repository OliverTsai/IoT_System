using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IoTMonitor.Api.Contracts;
using IoTMonitor.Api.Tests.Infrastructure;

namespace IoTMonitor.Api.Tests;

public sealed class SystemEndpointsTests(IoTMonitorApiFactory factory)
    : IClassFixture<IoTMonitorApiFactory>
{
    [Fact]
    public async Task SystemEndpoint_ReturnsServiceMetadata()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/system", cancellationToken);

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success but received {(int)response.StatusCode}: " +
            await response.Content.ReadAsStringAsync(cancellationToken));
        var payload = await response.Content.ReadFromJsonAsync<SystemInfoResponse>(cancellationToken);
        Assert.NotNull(payload);
        Assert.Equal("IoTMonitor.Api", payload.Name);
        Assert.Equal("v1", payload.ApiVersion);
    }

    [Fact]
    public async Task LiveHealthEndpoint_ReturnsHealthy()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/health/live", cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        Assert.Equal("Healthy", payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task OpenApiDocument_ContainsSystemEndpoint()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        Assert.True(payload.RootElement.GetProperty("paths").TryGetProperty("/api/system", out _));
    }

    [Fact]
    public async Task OpenApiDocument_DescribesDeviceAndTelemetryOperations()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        var paths = payload.RootElement.GetProperty("paths");
        var createDevice = paths
            .GetProperty("/api/devices")
            .GetProperty("post");
        var createDeviceResponses = createDevice.GetProperty("responses");

        Assert.True(createDevice.TryGetProperty("requestBody", out _));
        Assert.True(createDeviceResponses.TryGetProperty("201", out _));
        Assert.True(createDeviceResponses.TryGetProperty("400", out _));
        Assert.True(createDeviceResponses.TryGetProperty("409", out _));
        Assert.True(paths.TryGetProperty("/api/devices/{deviceId}", out _));
        Assert.True(paths.TryGetProperty("/api/devices/{deviceId}/status", out _));
        Assert.True(paths.TryGetProperty("/api/devices/{deviceId}/telemetry", out _));
        Assert.True(paths.TryGetProperty("/api/devices/{deviceId}/telemetry/latest", out _));
        Assert.True(paths.TryGetProperty("/api/auth/csrf", out _));
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/auth/me", out _));
        Assert.True(paths.TryGetProperty("/api/users", out _));
        Assert.True(paths.TryGetProperty("/api/alerts", out _));
        Assert.True(paths.TryGetProperty("/api/alerts/{alertId}/acknowledge", out _));
    }

    [Fact]
    public async Task MonitoringHub_NegotiationRequiresAuthentication()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsync(
            "/hubs/monitoring/negotiate?negotiateVersion=1",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CorsPreflight_AllowsConfiguredOriginWithCredentials()
    {
        using var client = factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/devices");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal(
            "true",
            response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task CorsPreflight_DoesNotTrustUnconfiguredOrigin()
    {
        using var client = factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/devices");
        request.Headers.Add("Origin", "https://untrusted.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task LoginEndpoint_RejectsRequestsBeyondConfiguredRateLimit()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { username = "rate-limit-test", password = "invalid" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "rate-limit-test", password = "invalid" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(
            "application/problem+json",
            rejected.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownEndpoint_ReturnsProblemDetails()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/not-found", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
