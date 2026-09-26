import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'

export interface Lookup {
  id: number
  name: string
}

export const useLookupsStore = defineStore('lookups', () => {
  const suppliers = ref<Lookup[]>([])
  const categories = ref<Lookup[]>([])
  const productFamilies = ref<Lookup[]>([])
  const loading = ref(false)

  async function fetchAll() {
    loading.value = true
    try {
      const [suppliersRes, categoriesRes, familiesRes] = await Promise.all([
        api.get<Lookup[]>('/suppliers'),
        api.get<Lookup[]>('/categories'),
        api.get<Lookup[]>('/productfamilies'),
      ])
      suppliers.value = suppliersRes.data
      categories.value = categoriesRes.data
      productFamilies.value = familiesRes.data
    } finally {
      loading.value = false
    }
  }

  return { suppliers, categories, productFamilies, loading, fetchAll }
})