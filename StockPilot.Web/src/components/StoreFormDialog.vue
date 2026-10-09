<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useStoresStore, type Store } from '@/stores/stores'
import { getApiErrorMessage } from '@/utils/apiError'

const props = defineProps<{
  visible: boolean
  store: Store | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const storesStore = useStoresStore()
const toast = useToast()

const form = ref({ name: '', code: '' })
const saving = ref(false)

const isEdit = computed(() => props.store !== null)
const isValid = computed(
  () => form.value.name.trim().length >= 2 && form.value.code.trim().length >= 2,
)

const dialogVisible = computed({
  get: () => props.visible,
  set: (value: boolean) => emit('update:visible', value),
})

// Κάθε φορά που ανοίγει το dialog, γεμίζουμε τη φόρμα από το store (edit) ή την αδειάζουμε (create)
watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      form.value = {
        name: props.store?.name ?? '',
        code: props.store?.code ?? '',
      }
    }
  },
)

async function save() {
  saving.value = true
  try {
    const input = { name: form.value.name.trim(), code: form.value.code.trim() }
    if (props.store) {
      await storesStore.updateStore(props.store.id, input)
    } else {
      await storesStore.createStore(input)
    }
    toast.add({
      severity: 'success',
      summary: isEdit.value ? 'Το κατάστημα ενημερώθηκε' : 'Το κατάστημα δημιουργήθηκε',
      life: 3000,
    })
    emit('saved')
    dialogVisible.value = false
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Αποτυχία αποθήκευσης',
      detail: getApiErrorMessage(error),
      life: 5000,
    })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog
    v-model:visible="dialogVisible"
    modal
    :header="isEdit ? 'Επεξεργασία καταστήματος' : 'Νέο κατάστημα'"
    :style="{ width: '28rem' }"
  >
    <div class="form">
      <div class="field">
        <label for="store-name">Όνομα</label>
        <InputText id="store-name" v-model="form.name" maxlength="100" fluid />
      </div>
      <div class="field">
        <label for="store-code">Κωδικός</label>
        <InputText id="store-code" v-model="form.code" maxlength="20" fluid />
        <small>Μετατρέπεται αυτόματα σε κεφαλαία και πρέπει να είναι μοναδικός.</small>
      </div>
    </div>

    <template #footer>
      <Button label="Άκυρο" severity="secondary" text @click="dialogVisible = false" />
      <Button label="Αποθήκευση" :loading="saving" :disabled="!isValid" @click="save" />
    </template>
  </Dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
</style>