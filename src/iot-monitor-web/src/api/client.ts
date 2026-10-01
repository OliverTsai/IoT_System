import type {
  AuthenticatedUser,
  CreateDeviceInput,
  Device,
  DeviceDetails,
  PagedResponse,
  ProblemDetails,
  Telemetry,
} from "./types";

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7080";
const apiBaseUrl = configuredBaseUrl.replace(/\/$/, "");

let csrfToken: string | null = null;
let unauthorizedHandler: (() => void) | null = null;

interface RequestOptions extends RequestInit {
  csrf?: boolean;
  notifyUnauthorized?: boolean;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}.`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  get validationMessages(): string[] {
    return Object.values(this.problem?.errors ?? {}).flat();
  }
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  if (!response.headers.get("content-type")?.includes("json")) {
    return null;
  }

  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

async function refreshCsrfToken(): Promise<string> {
  const response = await fetch(`${apiBaseUrl}/api/auth/csrf`, {
    credentials: "include",
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  const payload = (await response.json()) as { token: string };
  csrfToken = payload.token;
  return payload.token;
}

async function request<T>(
  path: string,
  options: RequestOptions = {},
  retryCsrf = true,
): Promise<T> {
  const { csrf = false, notifyUnauthorized = true, headers, ...requestInit } = options;
  const requestHeaders = new Headers(headers);
  requestHeaders.set("Accept", "application/json");

  if (requestInit.body && !requestHeaders.has("Content-Type")) {
    requestHeaders.set("Content-Type", "application/json");
  }

  if (csrf) {
    requestHeaders.set("X-CSRF-TOKEN", csrfToken ?? (await refreshCsrfToken()));
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...requestInit,
    headers: requestHeaders,
    credentials: "include",
  });

  if (response.status === 400 && csrf && retryCsrf) {
    const problem = await readProblem(response);
    if (problem?.title === "Invalid CSRF token") {
      csrfToken = null;
      return request<T>(path, options, false);
    }
    throw new ApiError(response.status, problem);
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    if (response.status === 401 && notifyUnauthorized) {
      unauthorizedHandler?.();
    }
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const apiClient = {
  setUnauthorizedHandler(handler: () => void) {
    unauthorizedHandler = handler;
  },

  async login(username: string, password: string): Promise<AuthenticatedUser> {
    await refreshCsrfToken();
    const user = await request<AuthenticatedUser>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
      csrf: true,
      notifyUnauthorized: false,
    });
    csrfToken = null;
    await refreshCsrfToken();
    return user;
  },

  async logout(): Promise<void> {
    await request<void>("/api/auth/logout", { method: "POST", csrf: true });
    csrfToken = null;
  },

  getCurrentUser(): Promise<AuthenticatedUser> {
    return request<AuthenticatedUser>("/api/auth/me", {
      notifyUnauthorized: false,
    });
  },

  getDevices(page = 1, pageSize = 20): Promise<PagedResponse<Device>> {
    return request<PagedResponse<Device>>(
      `/api/devices?page=${page}&pageSize=${pageSize}`,
    );
  },

  getDevice(deviceId: string): Promise<DeviceDetails> {
    return request<DeviceDetails>(`/api/devices/${encodeURIComponent(deviceId)}`);
  },

  createDevice(input: CreateDeviceInput): Promise<Device> {
    return request<Device>("/api/devices", {
      method: "POST",
      body: JSON.stringify(input),
      csrf: true,
    });
  },

  updateDeviceStatus(deviceId: string, isActive: boolean): Promise<Device> {
    return request<Device>(`/api/devices/${encodeURIComponent(deviceId)}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive }),
      csrf: true,
    });
  },

  getTelemetry(
    deviceId: string,
    options: { page?: number; pageSize?: number; fromUtc?: string; toUtc?: string } = {},
  ): Promise<PagedResponse<Telemetry>> {
    const search = new URLSearchParams({
      page: String(options.page ?? 1),
      pageSize: String(options.pageSize ?? 50),
    });
    if (options.fromUtc) search.set("fromUtc", options.fromUtc);
    if (options.toUtc) search.set("toUtc", options.toUtc);

    return request<PagedResponse<Telemetry>>(
      `/api/devices/${encodeURIComponent(deviceId)}/telemetry?${search}`,
    );
  },
};
