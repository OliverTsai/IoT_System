import { computed, reactive } from "vue";
import { ApiError, apiClient } from "../api/client";
import type { AuthenticatedUser } from "../api/types";

const state = reactive<{
  user: AuthenticatedUser | null;
  initialized: boolean;
  loading: boolean;
}>({
  user: null,
  initialized: false,
  loading: false,
});

async function initialize(force = false): Promise<void> {
  if (state.initialized && !force) return;

  state.loading = true;
  try {
    state.user = await apiClient.getCurrentUser();
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      state.user = null;
    } else {
      throw error;
    }
  } finally {
    state.initialized = true;
    state.loading = false;
  }
}

async function login(username: string, password: string): Promise<void> {
  state.loading = true;
  try {
    state.user = await apiClient.login(username, password);
    state.initialized = true;
  } finally {
    state.loading = false;
  }
}

async function logout(): Promise<void> {
  state.loading = true;
  try {
    await apiClient.logout();
  } finally {
    state.user = null;
    state.initialized = true;
    state.loading = false;
  }
}

function clear(): void {
  state.user = null;
  state.initialized = true;
}

export const authStore = {
  state,
  isAuthenticated: computed(() => state.user !== null),
  isAdmin: computed(() => state.user?.role === "Admin"),
  canWriteTelemetry: computed(
    () => state.user?.role === "Admin" || state.user?.role === "Operator",
  ),
  initialize,
  login,
  logout,
  clear,
};
