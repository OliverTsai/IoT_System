import { createRouter, createWebHistory } from "vue-router";
import { authStore } from "../stores/auth";
import AppShell from "../components/AppShell.vue";
import AlertsView from "../views/AlertsView.vue";
import DashboardView from "../views/DashboardView.vue";
import DeviceDetailView from "../views/DeviceDetailView.vue";
import DevicesView from "../views/DevicesView.vue";
import LoginView from "../views/LoginView.vue";
import NotFoundView from "../views/NotFoundView.vue";

declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    guestOnly?: boolean;
  }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/login",
      name: "login",
      component: LoginView,
      meta: { guestOnly: true },
    },
    {
      path: "/",
      component: AppShell,
      meta: { requiresAuth: true },
      children: [
        { path: "", name: "dashboard", component: DashboardView },
        { path: "devices", name: "devices", component: DevicesView },
        {
          path: "devices/:deviceId",
          name: "device-details",
          component: DeviceDetailView,
          props: true,
        },
        { path: "alerts", name: "alerts", component: AlertsView },
      ],
    },
    { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundView },
  ],
  scrollBehavior: () => ({ top: 0 }),
});

router.beforeEach(async (to) => {
  try {
    await authStore.initialize();
  } catch {
    if (to.meta.requiresAuth) {
      return { name: "login", query: { service: "unavailable" } };
    }
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated.value) {
    return {
      name: "login",
      query: { redirect: to.fullPath },
    };
  }

  if (to.meta.guestOnly && authStore.isAuthenticated.value) {
    return { name: "dashboard" };
  }

  return true;
});
