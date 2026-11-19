<script setup lang="ts">
import { onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import { useLowStockStore } from '@/stores/lowStock'
import { getApiErrorMessage } from '@/utils/apiError'

const lowStockStore = useLowStockStore()
const toast = useToast()

onMounted(async () => {
  try {
    await lowStockStore.fetchLowStock()
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Αποτυχία φόρτωσης',
      detail: getApiErrorMessage(error),
      life: 5000,
    })
  }
})
</script>

<template>
  <div class="low-stock-page">
    <div class="header">
      <h1>Χαμηλό απόθεμα</h1>
    </div>

    <DataTable :value="lowStockStore.items" :loading="lowStockStore.loading" stripedRows>
      <Column header="Προϊόν">
        <template #body="{ data }">
          {{ data.productName }} <small class="sku">({{ data.sku }})</small>
        </template>
      </Column>
      <Column field="storeName" header="Κατάστημα" />
      <Column field="quantity" header="Ποσότητα" />
      <Column field="reorderLevel" header="Όριο" />
      <Column header="Κατάσταση">
        <template #body="{ data }">
          <Tag v-if="data.quantity === 0" value="Εξαντλήθηκε" severity="danger" />
          <Tag v-else value="Χαμηλό" severity="warn" />
        </template>
      </Column>
      <template #empty>Δεν υπάρχουν προϊόντα κάτω από το όριο.</template>
    </DataTable>
  </div>
</template>

<style scoped>
.low-stock-page {
  padding: 2rem;
}
.header {
  margin-bottom: 1.5rem;
}
.sku {
  color: var(--p-text-muted-color);
}
</style>