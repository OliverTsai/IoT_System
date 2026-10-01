import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Alert, Telemetry } from "../api/types";

const signalR = vi.hoisted(() => {
  const eventHandlers = new Map<string, (payload: unknown) => void>();
  let reconnectingHandler: (() => void) | null = null;
  let reconnectedHandler: (() => void) | null = null;
  let closeHandler: (() => void) | null = null;

  const connection = {
    state: "Disconnected",
    start: vi.fn(async () => {
      connection.state = "Connected";
    }),
    stop: vi.fn(async () => {
      connection.state = "Disconnected";
      closeHandler?.();
    }),
    on: vi.fn((name: string, handler: (payload: unknown) => void) => {
      eventHandlers.set(name, handler);
    }),
    onreconnecting: vi.fn((handler: () => void) => {
      reconnectingHandler = handler;
    }),
    onreconnected: vi.fn((handler: () => void) => {
      reconnectedHandler = handler;
    }),
    onclose: vi.fn((handler: () => void) => {
      closeHandler = handler;
    }),
  };

  return {
    connection,
    eventHandlers,
    reconnecting: () => reconnectingHandler?.(),
    reconnected: () => reconnectedHandler?.(),
    reset: () => {
      connection.state = "Disconnected";
      connection.start.mockClear();
      connection.stop.mockClear();
      eventHandlers.clear();
      reconnectingHandler = null;
      reconnectedHandler = null;
      closeHandler = null;
    },
  };
});

vi.mock("@microsoft/signalr", () => ({
  HubConnectionState: {
    Connected: "Connected",
    Connecting: "Connecting",
    Disconnected: "Disconnected",
    Reconnecting: "Reconnecting",
  },
  LogLevel: { Warning: 3 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return signalR.connection; }
  },
}));

describe("monitoringRealtime", () => {
  beforeEach(() => {
    vi.resetModules();
    signalR.reset();
  });

  it("delivers server events and requests REST synchronization after reconnect", async () => {
    const { monitoringRealtime } = await import("./monitoring");
    const onTelemetry = vi.fn();
    const onAlertRaised = vi.fn();
    const onResynchronize = vi.fn();
    const unsubscribe = monitoringRealtime.subscribe({
      onTelemetry,
      onAlertRaised,
      onResynchronize,
    });

    await monitoringRealtime.start();
    expect(monitoringRealtime.state.status).toBe("connected");
    expect(onResynchronize).toHaveBeenCalledOnce();

    const telemetry: Telemetry = {
      id: 1,
      deviceId: "device-1",
      temperatureCelsius: 46,
      humidityPercent: 50,
      recordedAtUtc: "2026-10-01T10:00:00Z",
      receivedAtUtc: "2026-10-01T10:00:01Z",
    };
    const alert: Alert = {
      id: 2,
      deviceId: "device-1",
      deviceExternalId: "sensor-01",
      deviceName: "Sensor 01",
      type: "TemperatureOutOfRange",
      severity: "Critical",
      message: "Temperature is above the critical maximum.",
      occurredAtUtc: "2026-10-01T10:00:00Z",
      acknowledgedAtUtc: null,
    };

    signalR.eventHandlers.get("TelemetryReceived")?.(telemetry);
    signalR.eventHandlers.get("AlertRaised")?.(alert);
    signalR.reconnecting();
    expect(monitoringRealtime.state.status).toBe("reconnecting");
    signalR.reconnected();

    expect(onTelemetry).toHaveBeenCalledWith(telemetry);
    expect(onAlertRaised).toHaveBeenCalledWith(alert);
    expect(onResynchronize).toHaveBeenCalledTimes(2);
    expect(monitoringRealtime.state.status).toBe("connected");

    unsubscribe();
    await monitoringRealtime.stop();
  });
});
