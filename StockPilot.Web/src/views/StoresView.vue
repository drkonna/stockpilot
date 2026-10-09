<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import { useAuthStore } from '@/stores/auth'
import { useStoresStore, type Store } from '@/stores/stores'
import { getApiErrorMessage } from '@/utils/apiError'
import StoreFormDialog from '@/components/StoreFormDialog.vue'

const authStore = useAuthStore()
const storesStore = useStoresStore()
const confirm = useConfirm()
const toast = useToast()

const dialogVisible = ref(false)
const editingStore = ref<Store | null>(null)

function openCreateDialog() {
  editingStore.value = null
  dialogVisible.value = true
}

function openEditDialog(store: Store) {
  editingStore.value = store
  dialogVisible.value = true
}

function confirmDelete(store: Store) {
  confirm.require({
    message: `Θέλεις σίγουρα να διαγράψεις το κατάστημα "${store.name}";`,
    header: 'Επιβεβαίωση διαγραφής',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Διαγραφή',
    rejectLabel: 'Άκυρο',
    acceptProps: { severity: 'danger' },
    accept: async () => {
      try {
        await storesStore.deleteStore(store.id)
        toast.add({ severity: 'success', summary: 'Το κατάστημα διαγράφηκε', life: 3000 })
        await storesStore.fetchStores()
      } catch (error) {
        toast.add({
          severity: 'error',
          summary: 'Αποτυχία διαγραφής',
          detail: getApiErrorMessage(error),
          life: 5000,
        })
      }
    },
  })
}

function handleSaved() {
  storesStore.fetchStores()
}

onMounted(() => {
  storesStore.fetchStores()
})
</script>

<template>
  <div class="stores-page">
    <div class="header">
      <h1>Καταστήματα</h1>
      <div class="header-actions">
        <Button v-if="authStore.isAdmin" label="Νέο κατάστημα" @click="openCreateDialog" />
      </div>
    </div>

    <DataTable :value="storesStore.stores" :loading="storesStore.loading" stripedRows>
      <Column field="name" header="Όνομα" />
      <Column field="code" header="Κωδικός" />
      <Column header="Τύπος">
        <template #body="{ data }">
          <Tag v-if="data.isCentral" value="Κεντρικό" severity="success" />
        </template>
      </Column>
      <Column v-if="authStore.isAdmin" header="Ενέργειες">
        <template #body="{ data }">
          <Button icon="pi pi-pencil" text rounded @click="openEditDialog(data)" />
          <Button
            icon="pi pi-trash"
            text
            rounded
            severity="danger"
            :disabled="data.isCentral"
            @click="confirmDelete(data)"
          />
        </template>
      </Column>
    </DataTable>

    <StoreFormDialog
      v-model:visible="dialogVisible"
      :store="editingStore"
      @saved="handleSaved"
    />
  </div>
</template>

<style scoped>
.stores-page {
  padding: 2rem;
}
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
}
.header-actions {
  display: flex;
  gap: 0.75rem;
}
</style>