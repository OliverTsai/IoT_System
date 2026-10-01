import { createApp } from "vue";
import App from "./App.vue";
import { apiClient } from "./api/client";
import { router } from "./router";
import { authStore } from "./stores/auth";
import "./styles.css";

apiClient.setUnauthorizedHandler(() => {
  authStore.clear();
  if (router.currentRoute.value.name !== "login") {
    void router.push({
      name: "login",
      query: { redirect: router.currentRoute.value.fullPath, reason: "expired" },
    });
  }
});

createApp(App).use(router).mount("#app");
