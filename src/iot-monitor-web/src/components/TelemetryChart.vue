<script setup lang="ts">
import { computed } from "vue";
import type { Telemetry } from "../api/types";
import UiState from "./UiState.vue";

const props = defineProps<{
  readings: Telemetry[];
  metric: "temperatureCelsius" | "humidityPercent";
  label: string;
  unit: string;
  color: string;
}>();

const orderedReadings = computed(() =>
  [...props.readings].sort(
    (left, right) =>
      new Date(left.recordedAtUtc).getTime() - new Date(right.recordedAtUtc).getTime(),
  ),
);

const values = computed(() => orderedReadings.value.map((reading) => reading[props.metric]));
const minimum = computed(() => Math.min(...values.value));
const maximum = computed(() => Math.max(...values.value));

const points = computed(() => {
  if (values.value.length === 0) return "";

  const range = maximum.value - minimum.value || 1;
  const denominator = Math.max(values.value.length - 1, 1);
  return values.value
    .map((value, index) => {
      const x = 8 + (index / denominator) * 84;
      const y = 45 - ((value - minimum.value) / range) * 34;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    })
    .join(" ");
});

const latest = computed(() => values.value.at(-1));
</script>

<template>
  <article class="chart-card">
    <div class="chart-card__header">
      <div>
        <span class="eyebrow">{{ label }}</span>
        <strong v-if="latest !== undefined">{{ latest.toFixed(1) }}{{ unit }}</strong>
      </div>
      <span
        v-if="values.length"
        class="chart-range"
      >
        {{ minimum.toFixed(1) }}–{{ maximum.toFixed(1) }}{{ unit }}
      </span>
    </div>

    <div
      v-if="values.length >= 2"
      class="chart-wrap"
    >
      <svg
        viewBox="0 0 100 52"
        role="img"
        :aria-label="`${label}歷史趨勢`"
      >
        <line
          v-for="y in [11, 28, 45]"
          :key="y"
          x1="8"
          x2="92"
          :y1="y"
          :y2="y"
        />
        <polyline
          :points="points"
          :style="{ stroke: color }"
        />
        <circle
          v-for="(point, index) in points.split(' ')"
          :key="point"
          :cx="point.split(',')[0]"
          :cy="point.split(',')[1]"
          r="1.4"
          :style="{ fill: color }"
        >
          <title>
            {{ orderedReadings[index]?.[metric].toFixed(1) }}{{ unit }} ·
            {{ new Date(orderedReadings[index]?.recordedAtUtc ?? "").toLocaleString("zh-TW") }}
          </title>
        </circle>
      </svg>
    </div>
    <UiState
      v-else
      title="資料不足"
      description="至少需要兩筆量測才能繪製趨勢。"
      kind="empty"
    />
  </article>
</template>
