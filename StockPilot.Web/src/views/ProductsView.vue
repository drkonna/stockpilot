<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useProductsStore, type Product } from '@/stores/products'
import { useLookupsStore } from '@/stores/lookups'
import { useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import DataTable, { type DataTablePageEvent, type DataTableSortEvent }  from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import ProductFormDialog from '@/components/ProductFormDialog.vue'
import ProductStockDialog from '@/components/ProductStockDialog.vue'
import ProductImportDialog from '@/components/ProductImportDialog.vue'

const authStore = useAuthStore()
const productsStore = useProductsStore()
const lookupsStore = useLookupsStore()
const router = useRouter()
const confirm = useConfirm()
const stockDialogVisible = ref(false)
const stockProduct = ref<Product | null>(null)
const importVisible = ref(false)

const filters = reactive({
  search: '',
  minQuantity: null as number | null,
  maxQuantity: null as number | null,
  minPrice: null as number | null,
  maxPrice: null as number | null,
})

const pageSize = 10
const first = ref(0) // index of the first row of the current page (PrimeVue's "first")
const sortField = ref<string | undefined>(undefined)
const sortOrder = ref<number | undefined>(undefined) // 1 = ascending, -1 = descending

const dialogVisible = ref(false)
const editingProduct = ref<Product | null>(null)

async function loadProducts() {
  await productsStore.fetchProducts({
    search: filters.search || undefined,
    minQuantity: filters.minQuantity ?? undefined,
    maxQuantity: filters.maxQuantity ?? undefined,
    minPrice: filters.minPrice ?? undefined,
    maxPrice: filters.maxPrice ?? undefined,
    page: first.value / pageSize + 1,
    pageSize,
    sortBy: sortField.value ?? undefined,
    sortDir: sortOrder.value === -1 ? 'desc' : 'asc',
  })
}

// New filters always start from the first page
function applyFilters() {
  first.value = 0
  loadProducts()
}

// After a delete/save/import stay on the current page, unless it no longer exists
// (e.g. the only product of the last page was deleted).
async function reloadCurrentPage() {
  await loadProducts()
  if (first.value > 0 && first.value >= productsStore.totalCount) {
    first.value = Math.max(0, Math.floor((productsStore.totalCount - 1) / pageSize) * pageSize)
    await loadProducts()
  }
}

function onPage(event: DataTablePageEvent) {
  first.value = event.first
  loadProducts()
}

function onSort(event: DataTableSortEvent) {
  sortField.value = typeof event.sortField === 'string' ? event.sortField : undefined
  sortOrder.value = event.sortOrder ?? undefined
  first.value = 0
  loadProducts()
}

// New categories / suppliers / families may have been created by the import,
// so the dropdowns of the product form must be reloaded as well.
function handleImported() {
  reloadCurrentPage()
  lookupsStore.fetchAll()
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
      reloadCurrentPage()
    },
  })
}
function openStockDialog(product: Product) {
  stockProduct.value = product
  stockDialogVisible.value = true
}

function handleSaved() {
  reloadCurrentPage()
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
      <Button
        v-if="authStore.isAdmin"
        label="Εισαγωγή"
        icon="pi pi-file-import"
        severity="secondary"
        outlined
        @click="importVisible = true"
      />
    </div>

    <DataTable :value="productsStore.products" :loading="productsStore.loading" lazy paginator :rows="10" :first="first" :totalRecords="productsStore.totalCount" :sortField="sortField" :sortOrder="sortOrder" stripedRows @page="onPage"  @sort="onSort">
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
          <Button icon="pi pi-building" text rounded aria-label="Απόθεμα ανά κατάστημα" @click="openStockDialog(data)" />
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

    <ProductStockDialog
      v-model:visible="stockDialogVisible"
      :product="stockProduct"
      @saved="handleSaved"
    />

    <ProductImportDialog v-model:visible="importVisible" @imported="handleImported" />
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