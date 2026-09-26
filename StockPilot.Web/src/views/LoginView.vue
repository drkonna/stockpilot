<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'

const email = ref('')
const password = ref('')
const errorMessage = ref('')
const loading = ref(false)

const authStore = useAuthStore()
const router = useRouter()

async function handleSubmit() {
  errorMessage.value = ''
  loading.value = true
  try {
    await authStore.login({ email: email.value, password: password.value })
    router.push({ name: 'products' })
  } catch {
    errorMessage.value = 'Λάθος email ή κωδικός.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <h1>Σύνδεση</h1>
    <form @submit.prevent="handleSubmit">
      <div class="field">
        <label for="email">Email</label>
        <InputText id="email" v-model="email" type="email" required />
      </div>
      <div class="field">
        <label for="password">Κωδικός</label>
        <Password id="password" v-model="password" :feedback="false" toggleMask required />
      </div>
      <Message v-if="errorMessage" severity="error">{{ errorMessage }}</Message>
      <Button type="submit" label="Σύνδεση" :loading="loading" />
    </form>
    <p>Δεν έχεις λογαριασμό; <RouterLink to="/register">Εγγραφή</RouterLink></p>
  </div>
</template>

<style scoped>
.login-page {
  max-width: 360px;
  margin: 4rem auto;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
</style>