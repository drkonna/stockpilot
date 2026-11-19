import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import ProductStockDialog from '../ProductStockDialog.vue'
import api from '@/services/api'
import type { Product, ProductStock, StoreStock } from '@/stores/products'

// The real axios instance is replaced: no network, and we can inspect what the component sends.
vi.mock('@/services/api', () => ({ default: { get: vi.fn(), put: vi.fn() } }))

// PrimeVue's Dialog renders through a Teleport to <body>; a plain stand-in keeps the test simple.
const DialogStub = { template: '<div><slot /><slot name="footer" /></div>' }

// The auth store reads the role from the JWT payload, so a token with just a role is enough.
function fakeToken(role: string) {
  return `header.${btoa(JSON.stringify({ role }))}.signature`
}

function storeStock(overrides: Partial<StoreStock> = {}): StoreStock {
  return {
    storeId: 2,
    storeName: 'Main',
    storeCode: 'MAIN',
    isCentral: true,
    quantity: 5,
    reorderLevel: 10,
    isLow: true,
    ...overrides,
  }
}

function productStock(store: StoreStock): ProductStock {
  return { productId: 1, productName: 'Coffee', sku: 'C-1', totalQuantity: store.quantity, stores: [store] }
}

async function openDialog(store: StoreStock, role = 'Admin') {
  localStorage.setItem('token', fakeToken(role))
  vi.mocked(api.get).mockResolvedValue({ data: productStock(store) } as never)

  const wrapper = mount(ProductStockDialog, {
    props: { visible: false, product: { id: 1, name: 'Coffee' } as Product },
    global: { plugins: [createPinia(), PrimeVue, ToastService], stubs: { Dialog: DialogStub } },
  })
  // The component loads the stock when it becomes visible, so open it the same way.
  await wrapper.setProps({ visible: true })
  await flushPromises()
  return wrapper
}

const saveButton = (wrapper: Awaited<ReturnType<typeof openDialog>>) =>
  wrapper.findAllComponents(Button).find((b) => b.props('icon') === 'pi pi-check')!

describe('ProductStockDialog as Admin', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.resetAllMocks()
  })

  it('keeps the existing reorder level when only the quantity is changed', async () => {
    const wrapper = await openDialog(storeStock({ quantity: 5, reorderLevel: 10 }))
    vi.mocked(api.put).mockResolvedValue({ data: storeStock({ quantity: 7, reorderLevel: 10 }) } as never)

    // PrimeVue's InputNumber is driven through its v-model event: we test OUR logic, not PrimeVue's typing.
    const [quantityInput] = wrapper.findAllComponents(InputNumber)
    quantityInput!.vm.$emit('update:modelValue', 7)
    await flushPromises()
    await saveButton(wrapper).trigger('click')
    await flushPromises()

    expect(api.put).toHaveBeenCalledWith('/products/1/stock/2', { quantity: 7, reorderLevel: 10 })
  })

  it('sends null when the reorder level is cleared', async () => {
    const wrapper = await openDialog(storeStock({ quantity: 5, reorderLevel: 10 }))
    vi.mocked(api.put).mockResolvedValue({ data: storeStock({ reorderLevel: null, isLow: false }) } as never)

    const [, levelInput] = wrapper.findAllComponents(InputNumber)
    levelInput!.vm.$emit('update:modelValue', null)
    await flushPromises()
    await saveButton(wrapper).trigger('click')
    await flushPromises()

    expect(api.put).toHaveBeenCalledWith('/products/1/stock/2', { quantity: 5, reorderLevel: null })
  })

  it('enables the save button only after something changed', async () => {
    const wrapper = await openDialog(storeStock())
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()

    const [quantityInput] = wrapper.findAllComponents(InputNumber)
    quantityInput!.vm.$emit('update:modelValue', 99)
    await flushPromises()

    expect(saveButton(wrapper).attributes('disabled')).toBeUndefined()
  })

  it('shows the low-stock tag only for rows the API marks as low', async () => {
    const low = await openDialog(storeStock({ isLow: true }))
    expect(low.text()).toContain('Χαμηλό')

    const fine = await openDialog(storeStock({ quantity: 50, isLow: false }))
    expect(fine.text()).not.toContain('Χαμηλό')
  })
})

describe('ProductStockDialog as a regular user', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.resetAllMocks()
  })

  it('shows quantity and reorder level read-only', async () => {
    const wrapper = await openDialog(storeStock({ reorderLevel: 10 }), 'User')

    expect(wrapper.findAllComponents(InputNumber)).toHaveLength(0)
    expect(wrapper.text()).toContain('10')
  })
})