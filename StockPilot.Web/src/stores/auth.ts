import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
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

  function parseRole(jwt: string | null): string | null {
    if (!jwt) return null
    try {
      const payload = JSON.parse(atob(jwt.split('.')[1]!.replace(/-/g, '+').replace(/_/g, '/')))
      return (
        payload.role ??
        payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
        null
      )
    } catch {
      return null
    }
  }

  const role = computed(() => parseRole(token.value))
  const isAdmin = computed(() => role.value === 'Admin')

  function logout() {
    token.value = null
    localStorage.removeItem('token')
  }

  async function register(payload: LoginPayload) {
    await api.post('/auth/register', payload)
  }
  return { token, login, role, isAdmin, logout, register  }
})