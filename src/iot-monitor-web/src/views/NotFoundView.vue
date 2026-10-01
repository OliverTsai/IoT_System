<template>
  <main class="not-found-page">
    <span class="eyebrow">404 / NOT FOUND</span>
    <h1>這個頁面不存在</h1>
    <p>網址可能已變更，請回到系統總覽繼續操作。</p>
    <RouterLink
      class="button button--primary"
      :to="{ name: 'dashboard' }"
    >
      返回總覽
    </RouterLink>
  </main>
</template>
