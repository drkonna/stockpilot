import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'

export interface Store {
  id: number
  name: string
  code: string
  isCentral: boolean
}

export interface StoreInput {
  name: string
  code: string
}

export const useStoresStore = defineStore('stores', () => {
  const stores = ref<Store[]>([])
  const loading = ref(false)

  async function fetchStores() {
    loading.value = true
    try {
      const { data } = await api.get<Store[]>('/stores')
      stores.value = data
    } finally {
      loading.value = false
    }
  }

  async function createStore(input: StoreInput) {
    await api.post('/stores', input)
  }

  async function updateStore(id: number, input: StoreInput) {
    await api.put(`/stores/${id}`, input)
  }

  async function deleteStore(id: number) {
    await api.delete(`/stores/${id}`)
  }

  return { stores, loading, fetchStores, createStore, updateStore, deleteStore }
})