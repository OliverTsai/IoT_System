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
  lastSeenAtUtc: string | null;
  isOnline: boolean;
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

export type AlertType =
  | "TemperatureOutOfRange"
  | "HumidityOutOfRange"
  | "DeviceOffline";

export type AlertSeverity = "Information" | "Warning" | "Critical";

export interface Alert {
  id: number;
  deviceId: string;
  deviceExternalId: string;
  deviceName: string;
  type: AlertType;
  severity: AlertSeverity;
  message: string;
  occurredAtUtc: string;
  acknowledgedAtUtc: string | null;
}
