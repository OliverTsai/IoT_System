<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { RouterLink } from "vue-router";
import { ApiError, apiClient } from "../api/client";
import type { Alert, AlertSeverity, PagedResponse } from "../api/types";
import UiState from "../components/UiState.vue";
import { monitoringRealtime } from "../realtime/monitoring";
import { authStore } from "../stores/auth";
import { formatDateTime, formatRelativeTime } from "../utils/format";

type AcknowledgementFilter = "all" | "open" | "acknowledged";
type SeverityFilter = "all" | AlertSeverity;

const pageSize = 20;
const page = ref(1);
const acknowledgementFilter = ref<AcknowledgementFilter>("open");
const severityFilter = ref<SeverityFilter>("all");
const response = ref<PagedResponse<Alert> | null>(null);
const loading = ref(true);
const errorMessage = ref("");
const acknowledgeError = ref("");
const acknowledgingId = ref<number | null>(null);
const canAcknowledge = computed(
  () => authStore.state.user?.role === "Admin" || authStore.state.user?.role === "Operator",
);

let unsubscribe: () => void = () => undefined;
let resyncTimer: ReturnType<typeof setTimeout> | null = null;

const severityLabels: Record<AlertSeverity, string> = {
  Information: "資訊",
  Warning: "警告",
  Critical: "嚴重",
};

const typeLabels = {
  TemperatureOutOfRange: "溫度超出範圍",
  HumidityOutOfRange: "濕度超出範圍",
  DeviceOffline: "設備離線",
} as const;

function acknowledgedQuery(): boolean | undefined {
  if (acknowledgementFilter.value === "open") return false;
  if (acknowledgementFilter.value === "acknowledged") return true;
  return undefined;
}

function matchesSeverity(alert: Alert): boolean {
  return severityFilter.value === "all" || alert.severity === severityFilter.value;
}

async function loadAlerts(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";
  try {
    response.value = await apiClient.getAlerts({
      page: page.value,
      pageSize,
      severity: severityFilter.value === "all" ? undefined : severityFilter.value,
      acknowledged: acknowledgedQuery(),
    });
  } catch (error) {
    errorMessage.value = error instanceof ApiError
      ? error.message
      : "暫時無法讀取告警，請確認 API 是否正在執行。";
  } finally {
    loading.value = false;
  }
}

function scheduleResynchronize(): void {
  if (resyncTimer) clearTimeout(resyncTimer);
  resyncTimer = setTimeout(() => {
    resyncTimer = null;
    void loadAlerts();
  }, 150);
}

function handleAlertRaised(alert: Alert): void {
  if (acknowledgementFilter.value === "acknowledged" || !matchesSeverity(alert)) return;
  if (!response.value || page.value !== 1) {
    scheduleResynchronize();
    return;
  }

  if (response.value.items.some((item) => item.id === alert.id)) return;
  const totalCount = response.value.totalCount + 1;
  response.value = {
    ...response.value,
    items: [alert, ...response.value.items]
      .sort((left, right) => {
        const timeDifference = new Date(right.occurredAtUtc).getTime()
          - new Date(left.occurredAtUtc).getTime();
        return timeDifference || right.id - left.id;
      })
      .slice(0, pageSize),
    totalCount,
    totalPages: Math.ceil(totalCount / pageSize),
  };
}

function handleAlertAcknowledged(alert: Alert): void {
  if (!matchesSeverity(alert) || acknowledgementFilter.value !== "all") {
    scheduleResynchronize();
    return;
  }

  if (!response.value) return;
  response.value = {
    ...response.value,
    items: response.value.items.map((item) => item.id === alert.id ? alert : item),
  };
}

async function acknowledge(alert: Alert): Promise<void> {
  acknowledgeError.value = "";
  acknowledgingId.value = alert.id;
  try {
    const updated = await apiClient.acknowledgeAlert(alert.id);
    handleAlertAcknowledged(updated);
  } catch (error) {
    acknowledgeError.value = error instanceof ApiError
      ? error.message
      : "確認告警時發生未預期錯誤。";
  } finally {
    acknowledgingId.value = null;
  }
}

async function changePage(nextPage: number): Promise<void> {
  page.value = nextPage;
  await loadAlerts();
  window.scrollTo({ top: 0, behavior: "smooth" });
}

watch([acknowledgementFilter, severityFilter], () => {
  page.value = 1;
  void loadAlerts();
});

onMounted(() => {
  unsubscribe = monitoringRealtime.subscribe({
    onAlertRaised: handleAlertRaised,
    onAlertAcknowledged: handleAlertAcknowledged,
    onResynchronize: scheduleResynchronize,
  });
  void loadAlerts();
});

onBeforeUnmount(() => {
  unsubscribe();
  if (resyncTimer) clearTimeout(resyncTimer);
});
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <span class="eyebrow">ALERT CENTER</span>
        <h1>告警中心</h1>
        <p>集中追蹤並確認超出安全範圍的設備量測。</p>
      </div>
      <div
        class="live-indicator"
        :class="`live-indicator--${monitoringRealtime.state.status}`"
      >
        <span aria-hidden="true" />
        {{ monitoringRealtime.state.status === "connected" ? "即時更新中" : "等待即時連線" }}
      </div>
    </header>

    <section
      class="section-card alert-toolbar"
      aria-label="告警篩選"
    >
      <div
        class="segmented-control"
        aria-label="確認狀態"
      >
        <button
          v-for="option in [
            { value: 'open', label: '待確認' },
            { value: 'acknowledged', label: '已確認' },
            { value: 'all', label: '全部' },
          ] as const"
          :key="option.value"
          type="button"
          :class="{ active: acknowledgementFilter === option.value }"
          @click="acknowledgementFilter = option.value"
        >
          {{ option.label }}
        </button>
      </div>
      <label class="select-field">
        <span>嚴重程度</span>
        <select v-model="severityFilter">
          <option value="all">全部</option>
          <option value="Critical">嚴重</option>
          <option value="Warning">警告</option>
          <option value="Information">資訊</option>
        </select>
      </label>
      <button
        class="button button--secondary"
        type="button"
        :disabled="loading"
        @click="loadAlerts"
      >
        ↻ 更新
      </button>
    </section>

    <p
      v-if="acknowledgeError"
      class="form-error alert-action-error"
      role="alert"
    >
      {{ acknowledgeError }}
    </p>

    <UiState
      v-if="loading"
      title="正在讀取告警"
      description="同步最新告警與確認狀態。"
      kind="loading"
    />
    <UiState
      v-else-if="errorMessage"
      title="無法載入告警"
      :description="errorMessage"
      kind="error"
      @retry="loadAlerts"
    />
    <UiState
      v-else-if="!response?.items.length"
      title="沒有符合條件的告警"
      description="新告警會在量測超出設定閾值時即時出現在這裡。"
    />
    <template v-else>
      <div class="alert-list-summary">
        <span>共 {{ response.totalCount }} 筆</span>
        <span>第 {{ response.page }} / {{ response.totalPages }} 頁</span>
      </div>
      <section
        class="alert-list"
        aria-label="告警清單"
      >
        <article
          v-for="alert in response.items"
          :key="alert.id"
          class="alert-card"
          :class="[
            `alert-card--${alert.severity.toLowerCase()}`,
            { 'alert-card--acknowledged': alert.acknowledgedAtUtc },
          ]"
        >
          <div
            class="alert-card__icon"
            aria-hidden="true"
          >
            {{ alert.type === "TemperatureOutOfRange" ? "°" : alert.type === "HumidityOutOfRange" ? "◌" : "!" }}
          </div>
          <div class="alert-card__content">
            <div class="alert-card__heading">
              <div>
                <span
                  class="severity-label"
                  :class="`severity-label--${alert.severity.toLowerCase()}`"
                >
                  {{ severityLabels[alert.severity] }}
                </span>
                <strong>{{ typeLabels[alert.type] }}</strong>
              </div>
              <span
                class="alert-time"
                :title="formatDateTime(alert.occurredAtUtc)"
              >
                {{ formatRelativeTime(alert.occurredAtUtc) }}
              </span>
            </div>
            <p>{{ alert.message }}</p>
            <div class="alert-card__meta">
              <RouterLink :to="{ name: 'device-details', params: { deviceId: alert.deviceId } }">
                {{ alert.deviceName }} · {{ alert.deviceExternalId }}
              </RouterLink>
              <span v-if="alert.acknowledgedAtUtc">
                已於 {{ formatDateTime(alert.acknowledgedAtUtc) }} 確認
              </span>
            </div>
          </div>
          <button
            v-if="canAcknowledge && !alert.acknowledgedAtUtc"
            class="button button--secondary alert-card__action"
            type="button"
            :disabled="acknowledgingId === alert.id"
            @click="acknowledge(alert)"
          >
            {{ acknowledgingId === alert.id ? "處理中…" : "確認告警" }}
          </button>
          <span
            v-else-if="alert.acknowledgedAtUtc"
            class="acknowledged-mark"
          >✓ 已確認</span>
        </article>
      </section>

      <nav
        v-if="response.totalPages > 1"
        class="pagination"
        aria-label="告警分頁"
      >
        <button
          class="button button--secondary"
          type="button"
          :disabled="page <= 1 || loading"
          @click="changePage(page - 1)"
        >
          ← 上一頁
        </button>
        <span>{{ page }} / {{ response.totalPages }}</span>
        <button
          class="button button--secondary"
          type="button"
          :disabled="page >= response.totalPages || loading"
          @click="changePage(page + 1)"
        >
          下一頁 →
        </button>
      </nav>
    </template>
  </div>
</template>
