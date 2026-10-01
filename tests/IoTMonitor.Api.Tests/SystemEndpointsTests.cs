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

        response.EnsureSuccessStatusCode();
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
    public async Task UnknownEndpoint_ReturnsProblemDetails()
    {
        using var client = factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/not-found", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
