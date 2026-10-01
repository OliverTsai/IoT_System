import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { reactive } from "vue";
import { apiBaseUrl } from "../api/client";
import type { Alert, Telemetry } from "../api/types";

export type MonitoringConnectionStatus =
  | "connecting"
  | "connected"
  | "reconnecting"
  | "disconnected";

export interface MonitoringSubscriber {
  onTelemetry?: (telemetry: Telemetry) => void;
  onAlertRaised?: (alert: Alert) => void;
  onAlertAcknowledged?: (alert: Alert) => void;
  onResynchronize?: () => void;
}

const retryDelaysMilliseconds = [2_000, 5_000, 10_000, 30_000];
const subscribers = new Set<MonitoringSubscriber>();
const state = reactive<{
  status: MonitoringConnectionStatus;
  lastConnectedAt: Date | null;
  errorMessage: string;
}>({
  status: "disconnected",
  lastConnectedAt: null,
  errorMessage: "",
});

let connection: HubConnection | null = null;
let shouldRun = false;
let retryAttempt = 0;
let retryTimer: ReturnType<typeof setTimeout> | null = null;

function notify(action: (subscriber: MonitoringSubscriber) => void): void {
  for (const subscriber of subscribers) {
    action(subscriber);
  }
}

function buildConnection(): HubConnection {
  const nextConnection = new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/monitoring`, { withCredentials: true })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
    .configureLogging(LogLevel.Warning)
    .build();

  nextConnection.on("TelemetryReceived", (telemetry: Telemetry) => {
    notify((subscriber) => subscriber.onTelemetry?.(telemetry));
  });
  nextConnection.on("AlertRaised", (alert: Alert) => {
    notify((subscriber) => subscriber.onAlertRaised?.(alert));
  });
  nextConnection.on("AlertAcknowledged", (alert: Alert) => {
    notify((subscriber) => subscriber.onAlertAcknowledged?.(alert));
  });
  nextConnection.onreconnecting(() => {
    state.status = "reconnecting";
    state.errorMessage = "即時連線中斷，正在重新連線。";
  });
  nextConnection.onreconnected(() => {
    state.status = "connected";
    state.lastConnectedAt = new Date();
    state.errorMessage = "";
    retryAttempt = 0;
    notify((subscriber) => subscriber.onResynchronize?.());
  });
  nextConnection.onclose((error) => {
    state.status = "disconnected";
    state.errorMessage = error ? "即時連線已中斷，將持續重試。" : "";
    if (shouldRun) scheduleStart();
  });

  return nextConnection;
}

function scheduleStart(): void {
  if (!shouldRun || retryTimer) return;

  const delay = retryDelaysMilliseconds[
    Math.min(retryAttempt, retryDelaysMilliseconds.length - 1)
  ];
  retryAttempt += 1;
  retryTimer = setTimeout(() => {
    retryTimer = null;
    void start();
  }, delay);
}

async function start(): Promise<void> {
  shouldRun = true;
  connection ??= buildConnection();

  if (
    connection.state === HubConnectionState.Connected ||
    connection.state === HubConnectionState.Connecting ||
    connection.state === HubConnectionState.Reconnecting
  ) {
    return;
  }

  if (retryTimer) {
    clearTimeout(retryTimer);
    retryTimer = null;
  }

  state.status = "connecting";
  state.errorMessage = "";
  try {
    await connection.start();
    if (!shouldRun) {
      await connection.stop();
      return;
    }

    retryAttempt = 0;
    state.status = "connected";
    state.lastConnectedAt = new Date();
    notify((subscriber) => subscriber.onResynchronize?.());
  } catch {
    state.status = "disconnected";
    state.errorMessage = "即時連線暫時無法建立，將持續重試。";
    scheduleStart();
  }
}

async function stop(): Promise<void> {
  shouldRun = false;
  retryAttempt = 0;
  if (retryTimer) {
    clearTimeout(retryTimer);
    retryTimer = null;
  }

  if (connection && connection.state !== HubConnectionState.Disconnected) {
    await connection.stop();
  }
  state.status = "disconnected";
  state.errorMessage = "";
}

function subscribe(subscriber: MonitoringSubscriber): () => void {
  subscribers.add(subscriber);
  return () => subscribers.delete(subscriber);
}

export const monitoringRealtime = {
  state,
  start,
  stop,
  subscribe,
};
