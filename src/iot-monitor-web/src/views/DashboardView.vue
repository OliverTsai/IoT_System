<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { ApiError, apiClient } from "../api/client";
import type { Device, DeviceDetails } from "../api/types";
import MetricCard from "../components/MetricCard.vue";
import StatusBadge from "../components/StatusBadge.vue";
import UiState from "../components/UiState.vue";
import { authStore } from "../stores/auth";
import { formatNumber, formatRelativeTime } from "../utils/format";

const devices = ref<Device[]>([]);
const featuredDevices = ref<DeviceDetails[]>([]);
const totalCount = ref(0);
const loading = ref(true);
const errorMessage = ref("");

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
    const response = await apiClient.getDevices(1, 100);
    devices.value = response.items;
    totalCount.value = response.totalCount;

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

onMounted(loadDashboard);
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

      <section class="section-card alert-preview">
        <div
          class="alert-preview__icon"
          aria-hidden="true"
        >
          △
        </div>
        <div>
          <span class="eyebrow">ALERTS</span>
          <h2>告警規則將於下一階段啟用</h2>
          <p>目前不顯示模擬告警；階段 7 將由真實閾值規則及 API 提供資料。</p>
        </div>
        <RouterLink
          class="button button--ghost"
          :to="{ name: 'alerts' }"
        >
          了解狀態
        </RouterLink>
      </section>
    </template>
  </div>
</template>
