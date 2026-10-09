<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import { useToast } from 'primevue/usetoast'
import { useAuthStore } from '@/stores/auth'
import {
  useProductsStore,
  type Product,
  type ProductStock,
  type StoreStock,
} from '@/stores/products'
import { getApiErrorMessage } from '@/utils/apiError'

const props = defineProps<{
  visible: boolean
  product: Product | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const authStore = useAuthStore()
const productsStore = useProductsStore()
const toast = useToast()

const stock = ref<ProductStock | null>(null)
// Οι τιμές που πληκτρολογεί ο χρήστης, ανά storeId, ξεχωριστά από τις αποθηκευμένες
const drafts = ref<Record<number, number | null>>({})
const loading = ref(false)
const savingStoreId = ref<number | null>(null)

const dialogVisible = computed({
  get: () => props.visible,
  set: (value: boolean) => emit('update:visible', value),
})

const total = computed(() => stock.value?.stores.reduce((sum, s) => sum + s.quantity, 0) ?? 0)

watch(
  () => props.visible,
  async (visible) => {
    if (!visible || !props.product) return
    stock.value = null
    loading.value = true
    try {
      const data = await productsStore.fetchProductStock(props.product.id)
      stock.value = data
      drafts.value = Object.fromEntries(data.stores.map((s) => [s.storeId, s.quantity]))
    } catch (error) {
      toast.add({
        severity: 'error',
        summary: 'Αποτυχία φόρτωσης αποθέματος',
        detail: getApiErrorMessage(error),
        life: 5000,
      })
      dialogVisible.value = false
    } finally {
      loading.value = false
    }
  },
)

function isDirty(row: StoreStock) {
  const draft = drafts.value[row.storeId]
  return draft !== null && draft !== undefined && draft !== row.quantity
}

async function save(row: StoreStock) {
  const quantity = drafts.value[row.storeId]
  if (!props.product || quantity === null || quantity === undefined) return

  savingStoreId.value = row.storeId
  try {
    const updated = await productsStore.setStoreStock(props.product.id, row.storeId, quantity)
    const target = stock.value?.stores.find((s) => s.storeId === row.storeId)
    if (target) target.quantity = updated.quantity
    toast.add({ severity: 'success', summary: 'Το απόθεμα ενημερώθηκε', life: 3000 })
    emit('saved')
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Αποτυχία αποθήκευσης',
      detail: getApiErrorMessage(error),
      life: 5000,
    })
  } finally {
    savingStoreId.value = null
  }
}
</script>

<template>
  <Dialog
    v-model:visible="dialogVisible"
    modal
    :header="`Απόθεμα: ${product?.name ?? ''}`"
    :style="{ width: '40rem' }"
  >
    <DataTable :value="stock?.stores ?? []" :loading="loading" size="small">
      <Column header="Κατάστημα">
        <template #body="{ data }">
          {{ data.storeName }}
          <Tag v-if="data.isCentral" value="Κεντρικό" severity="success" class="central-tag" />
        </template>
      </Column>
      <Column field="storeCode" header="Κωδικός" />
      <Column header="Ποσότητα">
        <template #body="{ data }">
          <div v-if="authStore.isAdmin" class="qty-edit">
            <InputNumber v-model="drafts[data.storeId]" :min="0" :use-grouping="false" />
            <Button
              icon="pi pi-check"
              rounded
              text
              :disabled="!isDirty(data)"
              :loading="savingStoreId === data.storeId"
              @click="save(data)"
            />
          </div>
          <span v-else>{{ data.quantity }}</span>
        </template>
      </Column>
    </DataTable>

    <template #footer>
      <strong class="total">Σύνολο: {{ total }}</strong>
      <Button label="Κλείσιμο" severity="secondary" @click="dialogVisible = false" />
    </template>
  </Dialog>
</template>

<style scoped>
.qty-edit {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.central-tag {
  margin-left: 0.5rem;
}
.total {
  margin-right: auto;
}
</style>