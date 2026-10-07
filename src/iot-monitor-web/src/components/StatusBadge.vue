<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  active: boolean;
  online: boolean;
}>();

const status = computed(() => {
  if (!props.active) return "disabled";
  return props.online ? "online" : "offline";
});

const label = computed(() => ({
  disabled: "已停用",
  offline: "離線",
  online: "運作中",
})[status.value]);
</script>

<template>
  <span
    class="status-badge"
    :class="`status-badge--${status}`"
  >
    <span aria-hidden="true" />
    {{ label }}
  </span>
</template>
