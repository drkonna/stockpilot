import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'

interface LoginPayload {
  email: string
  password: string
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'))

  async function login(payload: LoginPayload) {
    const { data } = await api.post('/auth/login', payload)
    token.value = data.token
    localStorage.setItem('token', data.token)
  }

  function logout() {
    token.value = null
    localStorage.removeItem('token')
  }

  async function register(payload: LoginPayload) {
    await api.post('/auth/register', payload)
  }
  return { token, login, logout, register  }
})