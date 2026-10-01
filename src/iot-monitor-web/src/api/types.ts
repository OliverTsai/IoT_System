export type UserRole = "Viewer" | "Operator" | "Admin";

export interface AuthenticatedUser {
  id: string;
  username: string;
  role: UserRole;
  expiresAtUtc: string;
}

export interface Device {
  id: string;
  externalId: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface Telemetry {
  id: number;
  deviceId: string;
  temperatureCelsius: number;
  humidityPercent: number;
  recordedAtUtc: string;
  receivedAtUtc: string;
}

export interface DeviceDetails extends Device {
  latestTelemetry: Telemetry | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export interface CreateDeviceInput {
  externalId: string;
  name: string;
}
