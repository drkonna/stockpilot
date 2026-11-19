import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'

export interface LowStockItem {
  productId: number
  productName: string
  sku: string
  storeId: number
  storeName: string
  storeCode: string
  quantity: number
  reorderLevel: number
}

export const useLowStockStore = defineStore('lowStock', () => {
  const items = ref<LowStockItem[]>([])
  const loading = ref(false)

  async function fetchLowStock() {
    loading.value = true
    try {
      const { data } = await api.get<LowStockItem[]>('/stock/low')
      items.value = data
    } finally {
      loading.value = false
    }
  }

  return { items, loading, fetchLowStock }
})