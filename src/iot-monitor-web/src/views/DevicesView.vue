<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { RouterLink } from "vue-router";
import { ApiError, apiClient } from "../api/client";
import type { Device, PagedResponse, Telemetry } from "../api/types";
import StatusBadge from "../components/StatusBadge.vue";
import UiState from "../components/UiState.vue";
import { monitoringRealtime } from "../realtime/monitoring";
import { authStore } from "../stores/auth";
import { isDeviceOnline } from "../utils/devicePresence";
import { formatDateTime, formatRelativeTime } from "../utils/format";

const pageSize = 12;
const page = ref(1);
const response = ref<PagedResponse<Device> | null>(null);
const loading = ref(true);
const errorMessage = ref("");
const createOpen = ref(false);
const creating = ref(false);
const createError = ref("");
const form = reactive({ externalId: "", name: "" });
const nowMilliseconds = ref(Date.now());
let unsubscribe: () => void = () => undefined;
let presenceTimer: ReturnType<typeof setInterval> | null = null;

const devices = computed(() => response.value?.items ?? []);
const isAdmin = authStore.isAdmin;

async function loadDevices(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";
  try {
    response.value = await apiClient.getDevices(page.value, pageSize);
  } catch (error) {
    errorMessage.value = error instanceof ApiError
      ? error.message
      : "暫時無法讀取設備，請確認 API 是否正在執行。";
  } finally {
    loading.value = false;
  }
}

async function changePage(nextPage: number): Promise<void> {
  page.value = nextPage;
  await loadDevices();
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function closeCreate(): void {
  createOpen.value = false;
  createError.value = "";
  form.externalId = "";
  form.name = "";
}

async function createDevice(): Promise<void> {
  createError.value = "";
  const externalId = form.externalId.trim();
  const name = form.name.trim();

  if (!externalId || !name) {
    createError.value = "設備識別碼與名稱皆為必填。";
    return;
  }

  if (!/^[A-Za-z0-9][A-Za-z0-9._:-]*$/.test(externalId)) {
    createError.value = "識別碼需以英數字開頭，且只能包含英數字、.、_、:、-。";
    return;
  }

  creating.value = true;
  try {
    await apiClient.createDevice({ externalId, name });
    page.value = 1;
    closeCreate();
    await loadDevices();
  } catch (error) {
    if (error instanceof ApiError) {
      createError.value = error.validationMessages[0] ?? error.message;
    } else {
      createError.value = "建立設備時發生未預期錯誤。";
    }
  } finally {
    creating.value = false;
  }
}

function handleTelemetry(telemetry: Telemetry): void {
  if (!response.value) return;

  const index = response.value.items.findIndex((device) => device.id === telemetry.deviceId);
  if (index < 0) return;

  const current = response.value.items[index];
  if (!current) return;
  const currentLastSeen = current.lastSeenAtUtc
    ? Date.parse(current.lastSeenAtUtc)
    : Number.NEGATIVE_INFINITY;
  if (Date.parse(telemetry.receivedAtUtc) < currentLastSeen) return;

  const items = [...response.value.items];
  items[index] = {
    ...current,
    lastSeenAtUtc: telemetry.receivedAtUtc,
    isOnline: current.isActive,
  };
  response.value = { ...response.value, items };
  nowMilliseconds.value = Date.now();
}

onMounted(() => {
  unsubscribe = monitoringRealtime.subscribe({
    onTelemetry: handleTelemetry,
    onResynchronize: () => void loadDevices(),
  });
  presenceTimer = setInterval(() => {
    nowMilliseconds.value = Date.now();
  }, 1_000);
  void loadDevices();
});

onBeforeUnmount(() => {
  unsubscribe();
  if (presenceTimer) clearInterval(presenceTimer);
});
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <span class="eyebrow">DEVICE REGISTRY</span>
        <h1>設備管理</h1>
        <p>查看設備狀態、建立時間與遙測明細。</p>
      </div>
      <button
        v-if="isAdmin"
        class="button button--primary"
        type="button"
        @click="createOpen = !createOpen"
      >
        <span aria-hidden="true">＋</span>
        新增設備
      </button>
    </header>

    <section
      v-if="createOpen && isAdmin"
      class="section-card create-panel"
    >
      <div class="section-card__header">
        <div>
          <span class="eyebrow">ADMIN ACTION</span>
          <h2>登錄新設備</h2>
        </div>
        <button
          class="icon-close"
          type="button"
          aria-label="關閉新增設備表單"
          @click="closeCreate"
        >
          ×
        </button>
      </div>
      <form
        class="inline-form"
        @submit.prevent="createDevice"
      >
        <label class="field">
          <span>設備識別碼</span>
          <input
            v-model="form.externalId"
            maxlength="100"
            placeholder="例如：factory-a.sensor-01"
            :disabled="creating"
          >
        </label>
        <label class="field">
          <span>設備名稱</span>
          <input
            v-model="form.name"
            maxlength="200"
            placeholder="例如：一廠環境感測器"
            :disabled="creating"
          >
        </label>
        <button
          class="button button--primary inline-form__submit"
          type="submit"
          :disabled="creating"
        >
          {{ creating ? "建立中…" : "建立設備" }}
        </button>
      </form>
      <p
        v-if="createError"
        class="form-error"
        role="alert"
      >
        {{ createError }}
      </p>
    </section>

    <UiState
      v-if="loading"
      title="正在讀取設備"
      description="同步設備清單與狀態。"
      kind="loading"
    />
    <UiState
      v-else-if="errorMessage"
      title="無法載入設備"
      :description="errorMessage"
      kind="error"
      @retry="loadDevices"
    />
    <UiState
      v-else-if="devices.length === 0"
      title="目前沒有設備"
      :description="isAdmin ? '使用上方的新增設備按鈕建立第一台設備。' : '請聯絡 Admin 建立設備。'"
    />
    <template v-else>
      <section class="section-card table-card">
        <div class="table-summary">
          <span>共 {{ response?.totalCount }} 台設備</span>
          <span>第 {{ response?.page }} / {{ response?.totalPages }} 頁</span>
        </div>
        <div class="responsive-table">
          <table>
            <thead>
              <tr>
                <th>設備</th>
                <th>識別碼</th>
                <th>連線狀態</th>
                <th>管理狀態</th>
                <th>最後回報</th>
                <th>建立時間</th>
                <th><span class="sr-only">操作</span></th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="device in devices"
                :key="device.id"
              >
                <td data-label="設備">
                  <RouterLink
                    class="device-name-link"
                    :to="{ name: 'device-details', params: { deviceId: device.id } }"
                  >
                    <span
                      class="device-icon device-icon--small"
                      aria-hidden="true"
                    >◫</span>
                    <strong>{{ device.name }}</strong>
                  </RouterLink>
                </td>
                <td data-label="識別碼">
                  <code>{{ device.externalId }}</code>
                </td>
                <td data-label="連線狀態">
                  <StatusBadge
                    :active="device.isActive"
                    :online="isDeviceOnline(device, nowMilliseconds)"
                  />
                </td>
                <td data-label="管理狀態">
                  {{ device.isActive ? "已啟用" : "已停用" }}
                </td>
                <td data-label="最後回報">
                  {{ device.lastSeenAtUtc ? formatRelativeTime(device.lastSeenAtUtc) : "尚未回報" }}
                </td>
                <td data-label="建立時間">
                  {{ formatDateTime(device.createdAtUtc) }}
                </td>
                <td class="table-action">
                  <RouterLink
                    class="text-link"
                    :to="{ name: 'device-details', params: { deviceId: device.id } }"
                  >
                    查看 <span>→</span>
                  </RouterLink>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <nav
        v-if="(response?.totalPages ?? 0) > 1"
        class="pagination"
        aria-label="設備分頁"
      >
        <button
          class="button button--secondary"
          type="button"
          :disabled="page <= 1 || loading"
          @click="changePage(page - 1)"
        >
          ← 上一頁
        </button>
        <span>{{ page }} / {{ response?.totalPages }}</span>
        <button
          class="button button--secondary"
          type="button"
          :disabled="page >= (response?.totalPages ?? 1) || loading"
          @click="changePage(page + 1)"
        >
          下一頁 →
        </button>
      </nav>
    </template>
  </div>
</template>
