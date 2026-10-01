import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import type { Telemetry } from "../api/types";
import TelemetryChart from "./TelemetryChart.vue";

const readings: Telemetry[] = [
  {
    id: 2,
    deviceId: "device-1",
    temperatureCelsius: 26.8,
    humidityPercent: 58.2,
    recordedAtUtc: "2026-10-01T10:05:00Z",
    receivedAtUtc: "2026-10-01T10:05:01Z",
  },
  {
    id: 1,
    deviceId: "device-1",
    temperatureCelsius: 24.2,
    humidityPercent: 61.1,
    recordedAtUtc: "2026-10-01T10:00:00Z",
    receivedAtUtc: "2026-10-01T10:00:01Z",
  },
];

describe("TelemetryChart", () => {
  it("sorts readings and renders a trend line", () => {
    const wrapper = mount(TelemetryChart, {
      props: {
        readings,
        metric: "temperatureCelsius",
        label: "溫度趨勢",
        unit: "°C",
        color: "#dc7f3a",
      },
    });

    expect(wrapper.get("polyline").attributes("points")).not.toBe("");
    expect(wrapper.findAll("circle")).toHaveLength(2);
    expect(wrapper.text()).toContain("26.8°C");
    expect(wrapper.text()).toContain("24.2–26.8°C");
  });

  it("shows the empty state when there are not enough readings", () => {
    const wrapper = mount(TelemetryChart, {
      props: {
        readings: readings.slice(0, 1),
        metric: "humidityPercent",
        label: "濕度趨勢",
        unit: "%",
        color: "#2d77a6",
      },
    });

    expect(wrapper.find("polyline").exists()).toBe(false);
    expect(wrapper.text()).toContain("資料不足");
  });
});
