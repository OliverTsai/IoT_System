<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { RouterLink } from "vue-router";
import { ApiError, apiClient } from "../api/client";
import type { DeviceDetails, Telemetry } from "../api/types";
import MetricCard from "../components/MetricCard.vue";
import StatusBadge from "../components/StatusBadge.vue";
import TelemetryChart from "../components/TelemetryChart.vue";
import UiState from "../components/UiState.vue";
import { monitoringRealtime } from "../realtime/monitoring";
import { authStore } from "../stores/auth";
import { formatDateTime, formatRelativeTime } from "../utils/format";

const props = defineProps<{ deviceId: string }>();

const device = ref<DeviceDetails | null>(null);
const readings = ref<Telemetry[]>([]);
const loading = ref(true);
const changingStatus = ref(false);
const errorMessage = ref("");
const notFound = ref(false);
const statusError = ref("");
const isAdmin = authStore.isAdmin;
let unsubscribe: () => void = () => undefined;

const latest = computed(() => device.value?.latestTelemetry ?? null);

async function loadDevice(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";
  notFound.value = false;
  try {
    const [details, telemetry] = await Promise.all([
      apiClient.getDevice(props.deviceId),
      apiClient.getTelemetry(props.deviceId, { page: 1, pageSize: 50 }),
    ]);
    device.value = details;
    readings.value = telemetry.items;
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound.value = true;
      device.value = null;
    } else {
      errorMessage.value = error instanceof ApiError
        ? error.message
        : "暫時無法讀取設備明細。";
    }
  } finally {
    loading.value = false;
  }
}

async function toggleStatus(): Promise<void> {
  if (!device.value) return;

  changingStatus.value = true;
  statusError.value = "";
  try {
    const updated = await apiClient.updateDeviceStatus(device.value.id, !device.value.isActive);
    device.value = { ...device.value, ...updated };
  } catch (error) {
    statusError.value = error instanceof ApiError ? error.message : "無法更新設備狀態。";
  } finally {
    changingStatus.value = false;
  }
}

function handleTelemetry(telemetry: Telemetry): void {
  if (!device.value || telemetry.deviceId !== device.value.id) return;

  readings.value = [
    telemetry,
    ...readings.value.filter((reading) => reading.id !== telemetry.id),
  ]
    .sort((left, right) => {
      const timeDifference = new Date(right.recordedAtUtc).getTime()
        - new Date(left.recordedAtUtc).getTime();
      return timeDifference || right.id - left.id;
    })
    .slice(0, 50);

  const currentLatest = device.value.latestTelemetry;
  const recordedAtDifference = currentLatest
    ? new Date(telemetry.recordedAtUtc).getTime()
      - new Date(currentLatest.recordedAtUtc).getTime()
    : 1;
  if (
    !currentLatest ||
    recordedAtDifference > 0 ||
    (recordedAtDifference === 0 && telemetry.id > currentLatest.id)
  ) {
    device.value = { ...device.value, latestTelemetry: telemetry };
  }
}

watch(() => props.deviceId, loadDevice, { immediate: true });

onMounted(() => {
  unsubscribe = monitoringRealtime.subscribe({
    onTelemetry: handleTelemetry,
    onResynchronize: () => void loadDevice(),
  });
});

onBeforeUnmount(() => unsubscribe());
</script>

<template>
  <div class="page">
    <RouterLink
      class="back-link"
      :to="{ name: 'devices' }"
    >
      ← 返回設備清單
    </RouterLink>

    <UiState
      v-if="loading"
      title="正在讀取設備明細"
      description="同步設備資料與最近 50 筆量測。"
      kind="loading"
    />
    <UiState
      v-else-if="notFound"
      title="找不到這台設備"
      description="設備可能已不存在，或連結中的識別碼不正確。"
    />
    <UiState
      v-else-if="errorMessage"
      title="無法載入設備"
      :description="errorMessage"
      kind="error"
      @retry="loadDevice"
    />

    <template v-else-if="device">
      <header class="page-header device-heading">
        <div class="device-heading__identity">
          <div
            class="device-icon device-icon--large"
            aria-hidden="true"
          >
            ◫
          </div>
          <div>
            <span class="eyebrow">{{ device.externalId }}</span>
            <h1>{{ device.name }}</h1>
            <p>建立於 {{ formatDateTime(device.createdAtUtc) }}</p>
          </div>
        </div>
        <div class="device-heading__actions">
          <StatusBadge :active="device.isActive" />
          <button
            v-if="isAdmin"
            class="button button--secondary"
            type="button"
            :disabled="changingStatus"
            @click="toggleStatus"
          >
            {{ changingStatus ? "更新中…" : device.isActive ? "停用設備" : "啟用設備" }}
          </button>
        </div>
      </header>
      <p
        v-if="statusError"
        class="form-error"
        role="alert"
      >
        {{ statusError }}
      </p>

      <section
        class="metrics-grid metrics-grid--three"
        aria-label="最新量測"
      >
        <MetricCard
          label="最新溫度"
          :value="latest ? `${latest.temperatureCelsius.toFixed(1)}°C` : '—'"
          :hint="latest ? `量測於 ${formatRelativeTime(latest.recordedAtUtc)}` : '尚無量測資料'"
          tone="amber"
        />
        <MetricCard
          label="最新濕度"
          :value="latest ? `${latest.humidityPercent.toFixed(1)}%` : '—'"
          :hint="latest ? `接收於 ${formatRelativeTime(latest.receivedAtUtc)}` : '尚無量測資料'"
          tone="blue"
        />
        <MetricCard
          label="歷史資料"
          :value="readings.length"
          hint="顯示最近 50 筆量測"
          tone="teal"
        />
      </section>

      <section class="charts-grid">
        <TelemetryChart
          :readings="readings"
          metric="temperatureCelsius"
          label="溫度趨勢"
          unit="°C"
          color="#dc7f3a"
        />
        <TelemetryChart
          :readings="readings"
          metric="humidityPercent"
          label="濕度趨勢"
          unit="%"
          color="#2d77a6"
        />
      </section>

      <section class="section-card table-card">
        <div class="section-card__header">
          <div>
            <span class="eyebrow">TELEMETRY HISTORY</span>
            <h2>最近量測</h2>
          </div>
          <button
            class="button button--secondary"
            type="button"
            :disabled="loading"
            @click="loadDevice"
          >
            ↻ 更新
          </button>
        </div>
        <UiState
          v-if="readings.length === 0"
          title="尚無量測資料"
          description="啟動設備模擬器或透過 MQTT 發送資料後，量測會顯示在這裡。"
        />
        <div
          v-else
          class="responsive-table"
        >
          <table>
            <thead>
              <tr>
                <th>量測時間</th>
                <th>溫度</th>
                <th>濕度</th>
                <th>API 接收時間</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="reading in readings"
                :key="reading.id"
              >
                <td data-label="量測時間">
                  {{ formatDateTime(reading.recordedAtUtc) }}
                </td>
                <td data-label="溫度">
                  <strong>{{ reading.temperatureCelsius.toFixed(1) }}°C</strong>
                </td>
                <td data-label="濕度">
                  <strong>{{ reading.humidityPercent.toFixed(1) }}%</strong>
                </td>
                <td data-label="接收時間">
                  {{ formatDateTime(reading.receivedAtUtc) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>
