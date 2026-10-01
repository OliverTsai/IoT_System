<script setup lang="ts">
withDefaults(
  defineProps<{
    title: string;
    description: string;
    kind?: "empty" | "error" | "loading";
  }>(),
  { kind: "empty" },
);

defineEmits<{
  retry: [];
}>();
</script>

<template>
  <div
    class="ui-state"
    :class="`ui-state--${kind}`"
    role="status"
  >
    <div
      v-if="kind === 'loading'"
      class="spinner"
      aria-hidden="true"
    />
    <div
      v-else
      class="ui-state__icon"
      aria-hidden="true"
    >
      {{ kind === "error" ? "!" : "◇" }}
    </div>
    <strong>{{ title }}</strong>
    <p>{{ description }}</p>
    <button
      v-if="kind === 'error'"
      class="button button--secondary"
      type="button"
      @click="$emit('retry')"
    >
      再試一次
    </button>
  </div>
</template>
