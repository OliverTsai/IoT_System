<script setup lang="ts">
import { RouterLink } from "vue-router";
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <span class="eyebrow">ALERT CENTER</span>
        <h1>告警中心</h1>
        <p>集中追蹤超出安全範圍的設備量測。</p>
      </div>
      <span class="phase-badge">階段 7</span>
    </header>

    <section class="section-card coming-soon-card">
      <div
        class="coming-soon-card__visual"
        aria-hidden="true"
      >
        <span>△</span>
      </div>
      <div class="coming-soon-card__copy">
        <span class="eyebrow">NEXT CAPABILITY</span>
        <h2>真實告警資料尚未啟用</h2>
        <p>
          目前後端尚無告警規則與查詢 API，因此這裡不顯示模擬資料。下一階段會從遙測閾值產生告警，並透過 SignalR 即時推送至此頁。
        </p>
        <ul class="feature-checklist">
          <li><span>01</span> 可設定的溫度與濕度閾值</li>
          <li><span>02</span> 告警狀態、確認與歷史查詢</li>
          <li><span>03</span> SignalR 即時遙測與告警更新</li>
        </ul>
        <RouterLink
          class="button button--primary"
          :to="{ name: 'dashboard' }"
        >
          返回系統總覽
        </RouterLink>
      </div>
    </section>
  </div>
</template>
