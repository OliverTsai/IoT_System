import { describe, expect, it } from "vitest";
import { isDeviceOnline } from "./devicePresence";

const nowMilliseconds = Date.parse("2026-10-07T08:00:30.000Z");

describe("isDeviceOnline", () => {
  it("treats an enabled device with recent telemetry as online", () => {
    expect(isDeviceOnline({
      isActive: true,
      lastSeenAtUtc: "2026-10-07T08:00:01.000Z",
    }, nowMilliseconds)).toBe(true);
  });

  it("treats stale, missing, or disabled devices as offline", () => {
    expect(isDeviceOnline({
      isActive: true,
      lastSeenAtUtc: "2026-10-07T07:59:59.000Z",
    }, nowMilliseconds)).toBe(false);
    expect(isDeviceOnline({ isActive: true, lastSeenAtUtc: null }, nowMilliseconds)).toBe(false);
    expect(isDeviceOnline({
      isActive: false,
      lastSeenAtUtc: "2026-10-07T08:00:30.000Z",
    }, nowMilliseconds)).toBe(false);
  });
});
