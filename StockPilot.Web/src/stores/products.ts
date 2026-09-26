import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'

export interface Product {
  id: number
  name: string
  sku: string
  quantityInStock: number
  unitPrice: number
  createdAt: string
  categoryId: number | null
  categoryName: string | null
  supplierId: number
  supplierName: string
  productFamilyId: number | null
  productFamilyName: string | null
  color: string | null
  size: string | null
}

export interface ProductFilters {
  search?: string
  minQuantity?: number
  maxQuantity?: number
  minPrice?: number
  maxPrice?: number
}

export interface ProductInput {
  name: string
  sku: string
  quantityInStock: number
  unitPrice: number
  categoryId: number | null
  supplierId: number
  productFamilyId: number | null
  color: string | null
  size: string | null
}

export const useProductsStore = defineStore('products', () => {
  const products = ref<Product[]>([])
  const loading = ref(false)

  async function fetchProducts(filters: ProductFilters = {}) {
    loading.value = true
    try {
      const { data } = await api.get<Product[]>('/products', { params: filters })
      products.value = data
    } finally {
      loading.value = false
    }
  }

  async function createProduct(input: ProductInput) {
    await api.post('/products', input)
  }

  async function updateProduct(id: number, input: ProductInput) {
    await api.put(`/products/${id}`, input)
  }

  async function deleteProduct(id: number) {
    await api.delete(`/products/${id}`)
  }

  return { products, loading, fetchProducts, createProduct, updateProduct, deleteProduct }
})