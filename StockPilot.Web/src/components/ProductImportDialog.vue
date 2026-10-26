<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import { useToast } from 'primevue/usetoast'
import { useProductsStore, type ImportResult } from '@/stores/products'
import { getApiErrorMessage } from '@/utils/apiError'

const props = defineProps<{
  visible: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  imported: []
}>()

const productsStore = useProductsStore()
const toast = useToast()

const fileInput = ref<HTMLInputElement | null>(null)
const file = ref<File | null>(null)
const result = ref<ImportResult | null>(null)
const fileError = ref<string | null>(null) // the whole file was rejected (HTTP 400)
const busy = ref<'preview' | 'import' | null>(null)
const imported = ref(false) // a real import already ran for the selected file

const dialogVisible = computed({
  get: () => props.visible,
  set: (value: boolean) => emit('update:visible', value),
})

const canImport = computed(() => file.value !== null && busy.value === null && !imported.value)

// Every time the dialog opens, start from a clean state
watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      file.value = null
      result.value = null
      fileError.value = null
      imported.value = false
      if (fileInput.value) fileInput.value.value = ''
    }
  },
)

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  file.value = input.files?.[0] ?? null
  result.value = null
  fileError.value = null
  imported.value = false
}

async function run(dryRun: boolean) {
  if (!file.value) return

  busy.value = dryRun ? 'preview' : 'import'
  result.value = null
  fileError.value = null
  try {
    result.value = await productsStore.importProducts(file.value, dryRun)
    if (!dryRun) {
      imported.value = true
      toast.add({ severity: 'success', summary: 'Η εισαγωγή ολοκληρώθηκε', life: 3000 })
      emit('imported')
    }
  } catch (error) {
    fileError.value = getApiErrorMessage(error, 'Η εισαγωγή απέτυχε.')
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <Dialog v-model:visible="dialogVisible" modal header="Εισαγωγή προϊόντων" :style="{ width: '46rem' }">
    <div class="content">
      <p class="hint">
        Αρχείο <strong>.xlsx</strong>, <strong>.csv</strong> ή <strong>.txt</strong> με όνομα
        <code>ΚΩΔΙΚΟΣ_ΚΑΤΑΣΤΗΜΑΤΟΣ_ΗΗΜΜΕΕ</code> (π.χ. <code>MAIN_141026.xlsx</code>) και
        στήλες: Sku, Name, UnitPrice, Quantity, Category, Supplier, ProductFamily, Color, Size.
        Η τιμή αλλάζει μόνο από το αρχείο του κεντρικού καταστήματος.
      </p>

      <div class="picker">
        <input ref="fileInput" type="file" accept=".xlsx,.csv,.txt" hidden @change="onFileChange" />
        <Button
          label="Επιλογή αρχείου"
          icon="pi pi-upload"
          severity="secondary"
          outlined
          @click="fileInput?.click()"
        />
        <span v-if="file" class="file-name">{{ file.name }}</span>
      </div>

      <Message v-if="fileError" severity="error">{{ fileError }}</Message>

      <template v-if="result">
        <Message v-if="result.dryRun" severity="info">
          Προεπισκόπηση: δεν έχει αποθηκευτεί τίποτα.
        </Message>

        <div class="stats">
          <div class="stat">
            <span class="stat-value">{{ result.added }}</span>
            <span class="stat-label">Προστέθηκαν</span>
          </div>
          <div class="stat">
            <span class="stat-value">{{ result.updated }}</span>
            <span class="stat-label">Ενημερώθηκαν</span>
          </div>
          <div class="stat" :class="{ 'stat-failed': result.failed > 0 }">
            <span class="stat-value">{{ result.failed }}</span>
            <span class="stat-label">Απέτυχαν</span>
          </div>
        </div>

        <template v-if="result.errors.length > 0">
          <h4>Σφάλματα ανά γραμμή</h4>
          <DataTable :value="result.errors" size="small" stripedRows paginator :rows="10">
            <Column field="row" header="Γραμμή" style="width: 6rem" />
            <Column field="sku" header="SKU" style="width: 10rem" />
            <Column field="message" header="Σφάλμα" />
          </DataTable>
        </template>
      </template>
    </div>

    <template #footer>
      <Button label="Κλείσιμο" severity="secondary" text @click="dialogVisible = false" />
      <Button
        label="Προεπισκόπηση"
        severity="secondary"
        outlined
        :disabled="!file || busy !== null"
        :loading="busy === 'preview'"
        @click="run(true)"
      />
      <Button label="Εισαγωγή" :disabled="!canImport" :loading="busy === 'import'" @click="run(false)" />
    </template>
  </Dialog>
</template>

<style scoped>
.content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.hint {
  margin: 0;
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
}
.picker {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.file-name {
  font-weight: 500;
}
.stats {
  display: flex;
  gap: 1rem;
}
.stat {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0.5rem;
}
.stat-value {
  font-size: 1.75rem;
  font-weight: 600;
}
.stat-label {
  color: var(--p-text-muted-color);
}
.stat-failed .stat-value {
  color: var(--p-red-500);
}
h4 {
  margin: 0;
}
</style>