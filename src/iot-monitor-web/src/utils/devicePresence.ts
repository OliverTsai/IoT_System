import type { Device } from "../api/types";

export const deviceOnlineWindowMilliseconds = 30_000;

export function isDeviceOnline(
  device: Pick<Device, "isActive" | "lastSeenAtUtc">,
  nowMilliseconds = Date.now(),
): boolean {
  if (!device.isActive || !device.lastSeenAtUtc) return false;

  const lastSeenMilliseconds = Date.parse(device.lastSeenAtUtc);
  return Number.isFinite(lastSeenMilliseconds)
    && lastSeenMilliseconds >= nowMilliseconds - deviceOnlineWindowMilliseconds;
}
