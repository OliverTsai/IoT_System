using System.Net.Http.Json;
using IoTMonitor.Api.Contracts.Auth;

namespace IoTMonitor.Api.Tests.Infrastructure;

public static class HttpClientSecurityExtensions
{
    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    public static async Task RefreshCsrfTokenAsync(
        this HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync("/api/auth/csrf", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>(cancellationToken);
        Assert.NotNull(payload);

        client.DefaultRequestHeaders.Remove(CsrfHeaderName);
        client.DefaultRequestHeaders.Add(CsrfHeaderName, payload.Token);
    }

    public static async Task<HttpResponseMessage> LoginAsync(
        this HttpClient client,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        await client.RefreshCsrfTokenAsync(cancellationToken);
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            await client.RefreshCsrfTokenAsync(cancellationToken);
        }

        return response;
    }
}
