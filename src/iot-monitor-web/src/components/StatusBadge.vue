<script setup lang="ts">
defineProps<{
  active: boolean;
}>();
</script>

<template>
  <span
    class="status-badge"
    :class="active ? 'status-badge--active' : 'status-badge--inactive'"
  >
    <span aria-hidden="true" />
    {{ active ? "運作中" : "已停用" }}
  </span>
</template>
