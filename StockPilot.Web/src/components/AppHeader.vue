<script setup lang="ts">
import { RouterLink, useRouter } from 'vue-router'
import Button from 'primevue/button'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const router = useRouter()

function handleLogout() {
  authStore.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <header class="app-header">
    <nav class="nav">
      <RouterLink :to="{ name: 'products' }">Προϊόντα</RouterLink>
      <RouterLink :to="{ name: 'stores' }">Καταστήματα</RouterLink>
    </nav>
    <Button label="Αποσύνδεση" severity="secondary" @click="handleLogout" />
  </header>
</template>

<style scoped>
.app-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 2rem;
  border-bottom: 1px solid var(--p-content-border-color);
}
.nav {
  display: flex;
  gap: 1.5rem;
}
.nav a {
  color: var(--p-text-muted-color);
  text-decoration: none;
  font-weight: 500;
}
.nav a.router-link-active {
  color: var(--p-primary-color);
}
</style>