import { beforeEach, describe, expect, it, vi } from "vitest";

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("apiClient", () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it("uses cookies and refreshes the CSRF token around login", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ token: "before-login" }))
      .mockResolvedValueOnce(jsonResponse({
        id: "user-1",
        username: "admin",
        role: "Admin",
        expiresAtUtc: "2026-10-01T10:00:00Z",
      }))
      .mockResolvedValueOnce(jsonResponse({ token: "after-login" }));
    vi.stubGlobal("fetch", fetchMock);

    const { apiClient } = await import("./client");
    const user = await apiClient.login("admin", "correct horse battery staple");

    expect(user.role).toBe("Admin");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: "include" });

    const loginOptions = fetchMock.mock.calls[1]?.[1];
    expect(loginOptions).toMatchObject({ method: "POST", credentials: "include" });
    expect(new Headers(loginOptions?.headers).get("X-CSRF-TOKEN")).toBe("before-login");
  });

  it("notifies the application when an authenticated request returns 401", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({ title: "Unauthorized", status: 401 }, 401),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { ApiError, apiClient } = await import("./client");
    const onUnauthorized = vi.fn();
    apiClient.setUnauthorizedHandler(onUnauthorized);

    await expect(apiClient.getDevices()).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it("refreshes an expired CSRF token and retries once", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ token: "expired-token" }))
      .mockResolvedValueOnce(jsonResponse({ title: "Invalid CSRF token" }, 400))
      .mockResolvedValueOnce(jsonResponse({ token: "fresh-token" }))
      .mockResolvedValueOnce(jsonResponse({
        id: "device-1",
        externalId: "sensor-01",
        name: "Sensor 01",
        isActive: true,
        createdAtUtc: "2026-10-01T08:00:00Z",
      }, 201));
    vi.stubGlobal("fetch", fetchMock);

    const { apiClient } = await import("./client");
    await apiClient.createDevice({ externalId: "sensor-01", name: "Sensor 01" });

    expect(fetchMock).toHaveBeenCalledTimes(4);
    const retryOptions = fetchMock.mock.calls[3]?.[1];
    expect(new Headers(retryOptions?.headers).get("X-CSRF-TOKEN")).toBe("fresh-token");
  });

  it("queries and acknowledges alerts through the protected API", async () => {
    const alert = {
      id: 8,
      deviceId: "device-1",
      deviceExternalId: "sensor-01",
      deviceName: "Sensor 01",
      type: "TemperatureOutOfRange",
      severity: "Warning",
      message: "Temperature is above the warning maximum.",
      occurredAtUtc: "2026-10-01T10:00:00Z",
      acknowledgedAtUtc: null,
    };
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({
        items: [alert],
        page: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
      }))
      .mockResolvedValueOnce(jsonResponse({ token: "alert-csrf" }))
      .mockResolvedValueOnce(jsonResponse({
        ...alert,
        acknowledgedAtUtc: "2026-10-01T10:01:00Z",
      }));
    vi.stubGlobal("fetch", fetchMock);

    const { apiClient } = await import("./client");
    const result = await apiClient.getAlerts({ severity: "Warning", acknowledged: false });
    const acknowledged = await apiClient.acknowledgeAlert(alert.id);

    expect(result.totalCount).toBe(1);
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain(
      "severity=Warning&acknowledged=false",
    );
    expect(acknowledged.acknowledgedAtUtc).not.toBeNull();
    const acknowledgeOptions = fetchMock.mock.calls[2]?.[1];
    expect(acknowledgeOptions?.method).toBe("PATCH");
    expect(new Headers(acknowledgeOptions?.headers).get("X-CSRF-TOKEN")).toBe("alert-csrf");
  });
});
