<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ApiError } from "../api/client";
import { authStore } from "../stores/auth";

const route = useRoute();
const router = useRouter();

const username = ref("");
const password = ref("");
const errorMessage = ref("");

const serviceUnavailable = computed(() => route.query.service === "unavailable");
const sessionExpired = computed(() => route.query.reason === "expired");

async function submit(): Promise<void> {
  errorMessage.value = "";

  if (!username.value.trim() || !password.value) {
    errorMessage.value = "請輸入帳號與密碼。";
    return;
  }

  try {
    await authStore.login(username.value.trim(), password.value);
    const redirect = typeof route.query.redirect === "string"
      && route.query.redirect.startsWith("/")
      && !route.query.redirect.startsWith("//")
      ? route.query.redirect
      : "/";
    await router.replace(redirect);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      errorMessage.value = "帳號或密碼錯誤，請重新確認。";
    } else if (error instanceof ApiError && error.status === 429) {
      errorMessage.value = "登入嘗試過於頻繁，請稍後再試。";
    } else {
      errorMessage.value = "無法連線至服務，請確認 API 已啟動且憑證受信任。";
    }
  }
}
</script>

<template>
  <main class="login-page">
    <section
      class="login-visual"
      aria-label="IoT 監控系統介紹"
    >
      <div class="login-visual__content">
        <RouterLink
          class="brand brand--light"
          :to="{ name: 'login' }"
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
        <div class="login-copy">
          <span class="eyebrow eyebrow--light">CONNECTED OPERATIONS</span>
          <h1>讓設備狀態，<br>清楚可見。</h1>
          <p>集中查看設備、環境量測與運作狀態，從資料掌握現場脈動。</p>
        </div>
        <div
          class="signal-orbit"
          aria-hidden="true"
        >
          <span class="signal-orbit__ring signal-orbit__ring--one" />
          <span class="signal-orbit__ring signal-orbit__ring--two" />
          <span class="signal-orbit__core" />
          <span class="signal-node signal-node--one" />
          <span class="signal-node signal-node--two" />
          <span class="signal-node signal-node--three" />
        </div>
      </div>
    </section>

    <section class="login-panel">
      <form
        class="login-form"
        @submit.prevent="submit"
      >
        <div class="login-form__heading">
          <span class="eyebrow">SECURE ACCESS</span>
          <h2>登入控制台</h2>
          <p>使用系統管理員提供的帳號繼續。</p>
        </div>

        <div
          v-if="sessionExpired"
          class="notice notice--warning"
          role="status"
        >
          工作階段已過期，請重新登入。
        </div>
        <div
          v-if="serviceUnavailable"
          class="notice notice--error"
          role="alert"
        >
          目前無法連線至 API，請確認後端服務與 HTTPS 開發憑證。
        </div>
        <div
          v-if="errorMessage"
          class="notice notice--error"
          role="alert"
        >
          {{ errorMessage }}
        </div>

        <label class="field">
          <span>帳號</span>
          <input
            v-model="username"
            name="username"
            type="text"
            autocomplete="username"
            maxlength="100"
            placeholder="輸入帳號"
            :disabled="authStore.state.loading"
          >
        </label>

        <label class="field">
          <span>密碼</span>
          <input
            v-model="password"
            name="password"
            type="password"
            autocomplete="current-password"
            placeholder="輸入密碼"
            :disabled="authStore.state.loading"
          >
        </label>

        <button
          class="button button--primary button--wide"
          type="submit"
          :disabled="authStore.state.loading"
        >
          <span
            v-if="authStore.state.loading"
            class="button-spinner"
            aria-hidden="true"
          />
          {{ authStore.state.loading ? "登入中…" : "登入" }}
        </button>

        <p class="login-help">
          帳號由 Admin 建立；系統不提供公開註冊。
        </p>
      </form>
    </section>
  </main>
</template>
