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

export interface ProductQuery extends ProductFilters {
  page?: number
  pageSize?: number
  sortBy?: string
  sortDir?: 'asc' | 'desc'
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

export interface StoreStock {
  storeId: number
  storeName: string
  storeCode: string
  isCentral: boolean
  quantity: number
}

export interface ProductStock {
  productId: number
  productName: string
  sku: string
  totalQuantity: number
  stores: StoreStock[]
}

export interface ImportError {
  row: number
  sku: string | null
  message: string
}

export interface ImportResult {
  dryRun: boolean
  added: number
  updated: number
  failed: number
  errors: ImportError[]
}

export const useProductsStore = defineStore('products', () => {
  const products = ref<Product[]>([])
  const loading = ref(false)
  const totalCount = ref(0)

  // Only the newest request may update the state: if a slow older response arrives
  // after a newer one, it would otherwise overwrite the page the user is looking at.
  let latestRequest = 0

  async function fetchProducts(query: ProductQuery = {}) {
    const requestId = ++latestRequest
    loading.value = true
    try {
      const { data, headers } = await api.get<Product[]>('/products', { params: query })
      if (requestId !== latestRequest) return
      products.value = data
      const total = Number(headers['x-total-count'])
      totalCount.value = Number.isFinite(total) ? total : data.length
    } finally {
      if (requestId === latestRequest) loading.value = false
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

  async function fetchProductStock(productId: number) {
    const { data } = await api.get<ProductStock>(`/products/${productId}/stock`)
    return data
  }

  async function setStoreStock(productId: number, storeId: number, quantity: number) {
    const { data } = await api.put<StoreStock>(`/products/${productId}/stock/${storeId}`, { quantity })
    return data
  }

  async function importProducts(file: File, dryRun: boolean) {
    const form = new FormData()
    form.append('file', file) // "file" must match the parameter name of the endpoint
    const { data } = await api.post<ImportResult>('/products/import', form, { params: { dryRun } })
    return data
  }

  return { products, loading, totalCount, fetchProducts, createProduct, updateProduct, deleteProduct, fetchProductStock, setStoreStock, importProducts }
})