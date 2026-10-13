import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../api/client";
import { monitoringRealtime } from "../realtime/monitoring";
import { authStore } from "../stores/auth";
import DevicesView from "./DevicesView.vue";

describe("DevicesView", () => {
  afterEach(() => {
    authStore.clear();
  });

  it("builds the create form from the protocol catalog and submits its settings", async () => {
    authStore.state.user = {
      id: "admin-1",
      username: "admin",
      role: "Admin",
      expiresAtUtc: "2026-10-12T12:00:00Z",
    };
    vi.spyOn(monitoringRealtime, "subscribe").mockReturnValue(() => undefined);
    vi.spyOn(apiClient, "getDevices").mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 12,
      totalCount: 0,
      totalPages: 0,
    });
    vi.spyOn(apiClient, "getDeviceProtocols").mockResolvedValue([
      { key: "mqtt", displayName: "MQTT", fields: [], isDefault: true },
      {
        key: "test-tcp",
        displayName: "Test TCP",
        isDefault: false,
        fields: [{
          key: "host",
          displayName: "主機",
          inputType: "text",
          isRequired: true,
          minimum: null,
          maximum: null,
          placeholder: "device.local",
        }],
      },
    ]);
    const createDevice = vi.spyOn(apiClient, "createDevice").mockResolvedValue({
      id: "device-1",
      externalId: "sensor-01",
      name: "Sensor 01",
      protocolKey: "test-tcp",
      isActive: true,
      createdAtUtc: "2026-10-12T08:00:00Z",
      lastSeenAtUtc: null,
      isOnline: false,
    });
    const wrapper = mount(DevicesView, {
      global: {
        stubs: {
          RouterLink: { template: "<a><slot /></a>" },
        },
      },
    });
    await flushPromises();

    await wrapper.get("header .button--primary").trigger("click");
    const initialInputs = wrapper.findAll("form input");
    await initialInputs[0]!.setValue("sensor-01");
    await initialInputs[1]!.setValue("Sensor 01");
    await wrapper.get("form select").setValue("test-tcp");
    await wrapper.findAll("form input")[2]!.setValue("device.local");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(createDevice).toHaveBeenCalledWith({
      externalId: "sensor-01",
      name: "Sensor 01",
      protocolKey: "test-tcp",
      protocolSettings: { host: "device.local" },
    });

    wrapper.unmount();
  });
});
