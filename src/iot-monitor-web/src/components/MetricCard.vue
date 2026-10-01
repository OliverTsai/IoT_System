<script setup lang="ts">
defineProps<{
  label: string;
  value: string | number;
  hint: string;
  tone?: "teal" | "amber" | "blue" | "slate";
}>();
</script>

<template>
  <article
    class="metric-card"
    :class="`metric-card--${tone ?? 'slate'}`"
  >
    <div class="metric-card__top">
      <span>{{ label }}</span>
      <span
        class="metric-card__glyph"
        aria-hidden="true"
      />
    </div>
    <strong>{{ value }}</strong>
    <small>{{ hint }}</small>
  </article>
</template>
