<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { RouterLink, RouterView, useRouter } from "vue-router";
import { authStore } from "../stores/auth";
import { monitoringRealtime } from "../realtime/monitoring";

const router = useRouter();
const menuOpen = ref(false);
const signingOut = ref(false);

const roleLabels = {
  Admin: "系統管理員",
  Operator: "操作人員",
  Viewer: "檢視人員",
} as const;

async function signOut(): Promise<void> {
  signingOut.value = true;
  try {
    await monitoringRealtime.stop().catch(() => undefined);
    await authStore.logout();
    await router.push({ name: "login" });
  } finally {
    signingOut.value = false;
  }
}

function closeMenu(): void {
  menuOpen.value = false;
}

onMounted(() => {
  void monitoringRealtime.start();
});

onUnmounted(() => {
  void monitoringRealtime.stop();
});
</script>

<template>
  <div class="app-shell">
    <header class="mobile-header">
      <RouterLink
        class="brand"
        :to="{ name: 'dashboard' }"
        @click="closeMenu"
      >
        <span
          class="brand-mark"
          aria-hidden="true"
        ><span /></span>
        <span>IoT Monitor</span>
      </RouterLink>
      <button
        class="icon-button"
        type="button"
        :aria-expanded="menuOpen"
        aria-label="切換導覽選單"
        @click="menuOpen = !menuOpen"
      >
        <span /><span /><span />
      </button>
    </header>

    <aside
      class="sidebar"
      :class="{ 'sidebar--open': menuOpen }"
    >
      <RouterLink
        class="brand brand--desktop"
        :to="{ name: 'dashboard' }"
      >
        <span
          class="brand-mark"
          aria-hidden="true"
        ><span /></span>
        <span>
          <strong>IoT Monitor</strong>
          <small>Operations console</small>
        </span>
      </RouterLink>

      <nav
        class="primary-nav"
        aria-label="主要導覽"
      >
        <RouterLink
          :to="{ name: 'dashboard' }"
          @click="closeMenu"
        >
          <span
            class="nav-icon"
            aria-hidden="true"
          >⌁</span>
          總覽
        </RouterLink>
        <RouterLink
          :to="{ name: 'devices' }"
          @click="closeMenu"
        >
          <span
            class="nav-icon"
            aria-hidden="true"
          >▤</span>
          設備
        </RouterLink>
        <RouterLink
          :to="{ name: 'alerts' }"
          @click="closeMenu"
        >
          <span
            class="nav-icon"
            aria-hidden="true"
          >△</span>
          告警
        </RouterLink>
      </nav>

      <div class="sidebar-footer">
        <div
          class="connection-status"
          :class="`connection-status--${monitoringRealtime.state.status}`"
          :title="monitoringRealtime.state.errorMessage"
        >
          <span aria-hidden="true" />
          {{ monitoringRealtime.state.status === "connected" ? "即時連線正常" :
            monitoringRealtime.state.status === "reconnecting" ? "重新連線中" :
            monitoringRealtime.state.status === "connecting" ? "正在連線" : "即時連線中斷" }}
        </div>
        <div class="account-card">
          <div
            class="avatar"
            aria-hidden="true"
          >
            {{ authStore.state.user?.username.slice(0, 1).toUpperCase() }}
          </div>
          <div class="account-copy">
            <strong>{{ authStore.state.user?.username }}</strong>
            <span v-if="authStore.state.user">
              {{ roleLabels[authStore.state.user.role] }}
            </span>
          </div>
          <button
            class="account-logout"
            type="button"
            :disabled="signingOut"
            aria-label="登出"
            title="登出"
            @click="signOut"
          >
            ↗
          </button>
        </div>
      </div>
    </aside>

    <button
      v-if="menuOpen"
      class="sidebar-backdrop"
      type="button"
      aria-label="關閉導覽選單"
      @click="closeMenu"
    />

    <main class="app-content">
      <RouterView />
    </main>
  </div>
</template>
