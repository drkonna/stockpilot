<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useProductsStore, type Product } from '@/stores/products'
import { useLookupsStore } from '@/stores/lookups'
import { useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import ProductFormDialog from '@/components/ProductFormDialog.vue'

const authStore = useAuthStore()
const productsStore = useProductsStore()
const lookupsStore = useLookupsStore()
const router = useRouter()
const confirm = useConfirm()

const filters = reactive({
  search: '',
  minQuantity: null as number | null,
  maxQuantity: null as number | null,
  minPrice: null as number | null,
  maxPrice: null as number | null,
})

const dialogVisible = ref(false)
const editingProduct = ref<Product | null>(null)

function applyFilters() {
  productsStore.fetchProducts({
    search: filters.search || undefined,
    minQuantity: filters.minQuantity ?? undefined,
    maxQuantity: filters.maxQuantity ?? undefined,
    minPrice: filters.minPrice ?? undefined,
    maxPrice: filters.maxPrice ?? undefined,
  })
}

function clearFilters() {
  filters.search = ''
  filters.minQuantity = null
  filters.maxQuantity = null
  filters.minPrice = null
  filters.maxPrice = null
  applyFilters()
}

function openCreateDialog() {
  editingProduct.value = null
  dialogVisible.value = true
}

function openEditDialog(product: Product) {
  editingProduct.value = product
  dialogVisible.value = true
}

function confirmDelete(product: Product) {
  confirm.require({
    message: `Θέλεις σίγουρα να διαγράψεις το "${product.name}";`,
    header: 'Επιβεβαίωση διαγραφής',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Διαγραφή',
    rejectLabel: 'Άκυρο',
    acceptProps: { severity: 'danger' },
    accept: async () => {
      await productsStore.deleteProduct(product.id)
      applyFilters()
    },
  })
}

function handleSaved() {
  applyFilters()
}

function handleLogout() {
  authStore.logout()
  router.push({ name: 'login' })
}

onMounted(() => {
  productsStore.fetchProducts()
  lookupsStore.fetchAll()
})
</script>

<template>
  <div class="products-page">
    <div class="header">
      <h1>Products</h1>
      <Button label="Αποσύνδεση" severity="secondary" @click="handleLogout" />
    </div>

    <div class="filters">
      <InputText v-model="filters.search" placeholder="Αναζήτηση (όνομα/SKU)" @keyup.enter="applyFilters" />
      <InputNumber v-model="filters.minQuantity" placeholder="Ελάχ. απόθεμα" />
      <InputNumber v-model="filters.maxQuantity" placeholder="Μέγ. απόθεμα" />
      <InputNumber v-model="filters.minPrice" placeholder="Ελάχ. τιμή" mode="currency" currency="EUR" locale="el-GR" />
      <InputNumber v-model="filters.maxPrice" placeholder="Μέγ. τιμή" mode="currency" currency="EUR" locale="el-GR" />
      <Button label="Φίλτρο" @click="applyFilters" />
      <Button label="Καθαρισμός" severity="secondary" outlined @click="clearFilters" />
      <Button label="Νέο προϊόν" @click="openCreateDialog" />
    </div>

    <DataTable :value="productsStore.products" :loading="productsStore.loading" paginator :rows="10" stripedRows>
      <Column field="name" header="Όνομα" sortable />
      <Column field="sku" header="SKU" sortable />
      <Column field="quantityInStock" header="Απόθεμα" sortable />
      <Column field="unitPrice" header="Τιμή" sortable>
        <template #body="{ data }">{{ data.unitPrice.toFixed(2) }} €</template>
      </Column>
      <Column field="categoryName" header="Κατηγορία" />
      <Column field="supplierName" header="Προμηθευτής" />
      <Column field="productFamilyName" header="Product Family" />
      <Column field="color" header="Χρώμα" />
      <Column field="size" header="Μέγεθος" />
      <Column header="Ενέργειες">
        <template #body="{ data }">
          <Button icon="pi pi-pencil" text rounded @click="openEditDialog(data)" />
          <Button icon="pi pi-trash" text rounded severity="danger" @click="confirmDelete(data)" />
        </template>
      </Column>
    </DataTable>

    <ProductFormDialog
      v-model:visible="dialogVisible"
      :product="editingProduct"
      @saved="handleSaved"
    />
  </div>
</template>

<style scoped>
.products-page {
  padding: 2rem;
}
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
}
.filters {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-bottom: 1.5rem;
  align-items: center;
}
</style>