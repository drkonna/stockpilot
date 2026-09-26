<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'
import axios from 'axios'

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
    await authStore.register({ email: email.value, password: password.value })
    await authStore.login({ email: email.value, password: password.value })
    router.push({ name: 'products' })
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.data?.message) {
      errorMessage.value = error.response.data.message
    } else {
      errorMessage.value = 'Κάτι πήγε στραβά κατά την εγγραφή.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="register-page">
    <h1>Εγγραφή</h1>
    <form @submit.prevent="handleSubmit">
      <div class="field">
        <label for="email">Email</label>
        <InputText id="email" v-model="email" type="email" required />
      </div>
      <div class="field">
        <label for="password">Κωδικός</label>
        <Password id="password" v-model="password" toggleMask required />
      </div>
      <Message v-if="errorMessage" severity="error">{{ errorMessage }}</Message>
      <Button type="submit" label="Εγγραφή" :loading="loading" />
    </form>
    <p>Έχεις ήδη λογαριασμό; <RouterLink to="/login">Σύνδεση</RouterLink></p>
  </div>
</template>

<style scoped>
.register-page {
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