<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { ApiError, apiClient } from "../api/client";
import type { Alert, Device, DeviceDetails, Telemetry } from "../api/types";
import MetricCard from "../components/MetricCard.vue";
import StatusBadge from "../components/StatusBadge.vue";
import UiState from "../components/UiState.vue";
import { monitoringRealtime } from "../realtime/monitoring";
import { authStore } from "../stores/auth";
import { formatNumber, formatRelativeTime } from "../utils/format";

const devices = ref<Device[]>([]);
const featuredDevices = ref<DeviceDetails[]>([]);
const totalCount = ref(0);
const openAlerts = ref<Alert[]>([]);
const openAlertCount = ref(0);
const loading = ref(true);
const errorMessage = ref("");
let unsubscribe: () => void = () => undefined;
let resyncTimer: ReturnType<typeof setTimeout> | null = null;

const alertTypeLabels = {
  TemperatureOutOfRange: "溫度超出範圍",
  HumidityOutOfRange: "濕度超出範圍",
  DeviceOffline: "設備離線",
} as const;

const activeCount = computed(() => devices.value.filter((device) => device.isActive).length);
const latestReadings = computed(() =>
  featuredDevices.value
    .map((device) => device.latestTelemetry)
    .filter((reading) => reading !== null),
);
const averageTemperature = computed(() => {
  if (!latestReadings.value.length) return null;
  return (
    latestReadings.value.reduce((sum, reading) => sum + reading.temperatureCelsius, 0) /
    latestReadings.value.length
  );
});
const averageHumidity = computed(() => {
  if (!latestReadings.value.length) return null;
  return (
    latestReadings.value.reduce((sum, reading) => sum + reading.humidityPercent, 0) /
    latestReadings.value.length
  );
});

async function loadDashboard(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";
  try {
    const [response, alertResponse] = await Promise.all([
      apiClient.getDevices(1, 100),
      apiClient.getAlerts({ page: 1, pageSize: 3, acknowledged: false }),
    ]);
    devices.value = response.items;
    totalCount.value = response.totalCount;
    openAlerts.value = alertResponse.items;
    openAlertCount.value = alertResponse.totalCount;

    const detailResults = await Promise.allSettled(
      response.items.slice(0, 6).map((device) => apiClient.getDevice(device.id)),
    );
    featuredDevices.value = detailResults
      .filter(
        (result): result is PromiseFulfilledResult<DeviceDetails> => result.status === "fulfilled",
      )
      .map((result) => result.value);
  } catch (error) {
    errorMessage.value =
      error instanceof ApiError
        ? error.message
        : "暫時無法讀取設備狀態，請確認 API 是否正在執行。";
  } finally {
    loading.value = false;
  }
}

function handleTelemetry(telemetry: Telemetry): void {
  const index = featuredDevices.value.findIndex((device) => device.id === telemetry.deviceId);
  if (index < 0) return;

  const current = featuredDevices.value[index];
  if (!current) return;
  const recordedAtDifference = current.latestTelemetry
    ? new Date(telemetry.recordedAtUtc).getTime()
      - new Date(current.latestTelemetry.recordedAtUtc).getTime()
    : 1;
  if (current.latestTelemetry && (
    recordedAtDifference < 0 ||
    (recordedAtDifference === 0 && telemetry.id <= current.latestTelemetry.id)
  )) {
    return;
  }

  featuredDevices.value[index] = { ...current, latestTelemetry: telemetry };
}

function handleAlertRaised(alert: Alert): void {
  if (alert.acknowledgedAtUtc || openAlerts.value.some((item) => item.id === alert.id)) return;
  openAlertCount.value += 1;
  openAlerts.value = [alert, ...openAlerts.value]
    .sort((left, right) => {
      const timeDifference = new Date(right.occurredAtUtc).getTime()
        - new Date(left.occurredAtUtc).getTime();
      return timeDifference || right.id - left.id;
    })
    .slice(0, 3);
}

function handleAlertAcknowledged(alert: Alert): void {
  if (!openAlerts.value.some((item) => item.id === alert.id)) {
    scheduleResynchronize();
    return;
  }

  openAlertCount.value = Math.max(0, openAlertCount.value - 1);
  openAlerts.value = openAlerts.value.filter((item) => item.id !== alert.id);
  scheduleResynchronize();
}

function scheduleResynchronize(): void {
  if (resyncTimer) clearTimeout(resyncTimer);
  resyncTimer = setTimeout(() => {
    resyncTimer = null;
    void loadDashboard();
  }, 150);
}

onMounted(() => {
  unsubscribe = monitoringRealtime.subscribe({
    onTelemetry: handleTelemetry,
    onAlertRaised: handleAlertRaised,
    onAlertAcknowledged: handleAlertAcknowledged,
    onResynchronize: scheduleResynchronize,
  });
  void loadDashboard();
});

onBeforeUnmount(() => {
  unsubscribe();
  if (resyncTimer) clearTimeout(resyncTimer);
});
</script>

<template>
  <div class="page page--dashboard">
    <header class="page-header dashboard-heading">
      <div>
        <span class="eyebrow">OPERATIONS OVERVIEW</span>
        <h1>早安，{{ authStore.state.user?.username }}</h1>
        <p>掌握設備連線狀態與最新環境量測。</p>
      </div>
      <button
        class="button button--secondary"
        type="button"
        :disabled="loading"
        @click="loadDashboard"
      >
        <span aria-hidden="true">↻</span>
        更新資料
      </button>
    </header>

    <UiState
      v-if="loading"
      title="正在同步設備資料"
      description="從 IoT Monitor API 取得最新狀態。"
      kind="loading"
    />
    <UiState
      v-else-if="errorMessage"
      title="無法載入總覽"
      :description="errorMessage"
      kind="error"
      @retry="loadDashboard"
    />
    <template v-else>
      <section
        class="metrics-grid"
        aria-label="系統摘要"
      >
        <MetricCard
          label="設備總數"
          :value="totalCount"
          hint="已登錄設備"
          tone="blue"
        />
        <MetricCard
          label="運作中"
          :value="activeCount"
          :hint="`目前頁面 ${devices.length} 台設備`"
          tone="teal"
        />
        <MetricCard
          label="平均溫度"
          :value="averageTemperature === null ? '—' : `${formatNumber(averageTemperature)}°C`"
          :hint="`取樣 ${latestReadings.length} 台最新量測`"
          tone="amber"
        />
        <MetricCard
          label="平均濕度"
          :value="averageHumidity === null ? '—' : `${formatNumber(averageHumidity)}%`"
          :hint="`取樣 ${latestReadings.length} 台最新量測`"
          tone="slate"
        />
      </section>

      <section class="section-card">
        <div class="section-card__header">
          <div>
            <span class="eyebrow">LIVE DEVICES</span>
            <h2>最近設備</h2>
          </div>
          <RouterLink
            class="text-link"
            :to="{ name: 'devices' }"
          >
            查看全部 <span>→</span>
          </RouterLink>
        </div>

        <UiState
          v-if="featuredDevices.length === 0"
          title="尚未建立設備"
          description="由 Admin 建立第一台設備後，狀態會顯示在這裡。"
        />
        <div
          v-else
          class="device-preview-grid"
        >
          <RouterLink
            v-for="device in featuredDevices"
            :key="device.id"
            class="device-preview"
            :to="{ name: 'device-details', params: { deviceId: device.id } }"
          >
            <div class="device-preview__header">
              <div
                class="device-icon"
                aria-hidden="true"
              >
                ◫
              </div>
              <StatusBadge :active="device.isActive" />
            </div>
            <strong>{{ device.name }}</strong>
            <span class="device-id">{{ device.externalId }}</span>
            <div
              v-if="device.latestTelemetry"
              class="device-reading"
            >
              <span>
                <small>溫度</small>
                <b>{{ device.latestTelemetry.temperatureCelsius.toFixed(1) }}°C</b>
              </span>
              <span>
                <small>濕度</small>
                <b>{{ device.latestTelemetry.humidityPercent.toFixed(1) }}%</b>
              </span>
            </div>
            <p
              v-else
              class="muted"
            >
              尚無量測資料
            </p>
            <small
              v-if="device.latestTelemetry"
              class="last-seen"
            >
              更新於 {{ formatRelativeTime(device.latestTelemetry.recordedAtUtc) }}
            </small>
          </RouterLink>
        </div>
      </section>

      <section class="section-card dashboard-alerts">
        <div class="section-card__header">
          <div>
            <span class="eyebrow">ACTIVE ALERTS</span>
            <h2>待確認告警 <span v-if="openAlertCount">{{ openAlertCount }}</span></h2>
          </div>
          <RouterLink
            class="text-link"
            :to="{ name: 'alerts' }"
          >
            前往告警中心 <span>→</span>
          </RouterLink>
        </div>
        <UiState
          v-if="openAlerts.length === 0"
          title="目前沒有待確認告警"
          description="量測超出設定閾值時，告警會即時顯示。"
        />
        <div
          v-else
          class="dashboard-alert-list"
        >
          <RouterLink
            v-for="alert in openAlerts"
            :key="alert.id"
            class="dashboard-alert-item"
            :class="`dashboard-alert-item--${alert.severity.toLowerCase()}`"
            :to="{ name: 'device-details', params: { deviceId: alert.deviceId } }"
          >
            <span
              class="dashboard-alert-item__mark"
              aria-hidden="true"
            >!</span>
            <span>
              <strong>{{ alertTypeLabels[alert.type] }}</strong>
              <small>{{ alert.deviceName }} · {{ formatRelativeTime(alert.occurredAtUtc) }}</small>
            </span>
            <span aria-hidden="true">→</span>
          </RouterLink>
        </div>
      </section>
    </template>
  </div>
</template>
