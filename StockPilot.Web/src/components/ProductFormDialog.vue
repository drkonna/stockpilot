<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useProductsStore, type Product, type ProductInput } from '@/stores/products'
import { useLookupsStore } from '@/stores/lookups'

const props = defineProps<{
  visible: boolean
  product: Product | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const productsStore = useProductsStore()
const lookupsStore = useLookupsStore()

const form = reactive({
  name: '',
  sku: '',
  quantityInStock: 0,
  unitPrice: 0,
  categoryId: null as number | null,
  supplierId: null as number | null,
  productFamilyId: null as number | null,
  color: '',
  size: '',
})

const saving = ref(false)
const errorMessage = ref('')

watch(
  () => props.visible,
  (isVisible) => {
    if (!isVisible) return
    errorMessage.value = ''
    if (props.product) {
      form.name = props.product.name
      form.sku = props.product.sku
      form.quantityInStock = props.product.quantityInStock
      form.unitPrice = props.product.unitPrice
      form.categoryId = props.product.categoryId
      form.supplierId = props.product.supplierId
      form.productFamilyId = props.product.productFamilyId
      form.color = props.product.color ?? ''
      form.size = props.product.size ?? ''
    } else {
      form.name = ''
      form.sku = ''
      form.quantityInStock = 0
      form.unitPrice = 0
      form.categoryId = null
      form.supplierId = null
      form.productFamilyId = null
      form.color = ''
      form.size = ''
    }
  },
)

async function handleSave() {
  errorMessage.value = ''
  if (!form.supplierId) {
    errorMessage.value = 'Επίλεξε προμηθευτή.'
    return
  }

  const input: ProductInput = {
    name: form.name,
    sku: form.sku,
    quantityInStock: form.quantityInStock,
    unitPrice: form.unitPrice,
    categoryId: form.categoryId,
    supplierId: form.supplierId,
    productFamilyId: form.productFamilyId,
    color: form.color.trim() === '' ? null : form.color.trim(),
    size: form.size.trim() === '' ? null : form.size.trim(),
  }

  saving.value = true
  try {
    if (props.product) {
      await productsStore.updateProduct(props.product.id, input)
    } else {
      await productsStore.createProduct(input)
    }
    emit('saved')
    emit('update:visible', false)
  } catch {
    errorMessage.value = 'Κάτι πήγε στραβά κατά την αποθήκευση.'
  } finally {
    saving.value = false
  }
}

function handleCancel() {
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    @update:visible="(v) => emit('update:visible', v)"
    modal
    :header="product ? 'Επεξεργασία προϊόντος' : 'Νέο προϊόν'"
    :style="{ width: '30rem' }"
  >
    <div class="form-grid">
      <div class="field">
        <label for="name">Όνομα</label>
        <InputText id="name" v-model="form.name" />
      </div>
      <div class="field">
        <label for="sku">SKU</label>
        <InputText id="sku" v-model="form.sku" />
      </div>
      <div class="field">
        <label for="quantity">Απόθεμα</label>
        <InputNumber id="quantity" v-model="form.quantityInStock" :min="0" />
      </div>
      <div class="field">
        <label for="price">Τιμή</label>
        <InputNumber id="price" v-model="form.unitPrice" mode="currency" currency="EUR" locale="el-GR" />
      </div>
      <div class="field">
        <label for="supplier">Προμηθευτής</label>
        <Select
          id="supplier"
          v-model="form.supplierId"
          :options="lookupsStore.suppliers"
          optionLabel="name"
          optionValue="id"
          placeholder="Επίλεξε προμηθευτή"
        />
      </div>
      <div class="field">
        <label for="category">Κατηγορία</label>
        <Select
          id="category"
          v-model="form.categoryId"
          :options="lookupsStore.categories"
          optionLabel="name"
          optionValue="id"
          placeholder="Επίλεξε κατηγορία"
          showClear
        />
      </div>
      <div class="field">
        <label for="family">Product Family</label>
        <Select
          id="family"
          v-model="form.productFamilyId"
          :options="lookupsStore.productFamilies"
          optionLabel="name"
          optionValue="id"
          placeholder="Επίλεξε family"
          showClear
        />
      </div>
      <div class="field">
        <label for="color">Χρώμα</label>
        <InputText id="color" v-model="form.color" />
      </div>
      <div class="field">
        <label for="size">Μέγεθος</label>
        <InputText id="size" v-model="form.size" />
      </div>
    </div>

    <Message v-if="errorMessage" severity="error">{{ errorMessage }}</Message>

    <template #footer>
      <Button label="Άκυρο" severity="secondary" outlined @click="handleCancel" />
      <Button label="Αποθήκευση" :loading="saving" @click="handleSave" />
    </template>
  </Dialog>
</template>

<style scoped>
.form-grid {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
</style>