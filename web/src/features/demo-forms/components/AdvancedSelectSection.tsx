import { Network } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AsyncCombobox,
  CascadingSelect,
  ComboboxInput,
  CustomerPicker,
  ErpCodeField,
  ProductPicker,
  TreeSelect,
  type SelectOption,
} from '@/components/common/inputs'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'
import {
  ADDRESS_LEVELS,
  CATALOGUE_OPTIONS,
  CHART_OF_ACCOUNTS,
  searchCustomers,
} from '@/features/demo-forms/data/options'
import type { EntityRef } from '@/types/entity'

export function AdvancedSelectSection() {
  const { t } = useTranslation('demo-forms')
  const [address, setAddress] = useState<(string | null)[]>(['34', 'kadikoy', null])
  const [customer, setCustomer] = useState<SelectOption | null>(null)
  const [products, setProducts] = useState<SelectOption[]>(CATALOGUE_OPTIONS)
  const [product, setProduct] = useState<string | null>(null)
  const [account, setAccount] = useState<string | null>('600.02')
  const [customerEntity, setCustomerEntity] = useState<EntityRef | null>(null)
  const [productEntity, setProductEntity] = useState<EntityRef | null>(null)
  const [erpCodeEntity, setErpCodeEntity] = useState<EntityRef | null>(null)

  /** A real app would POST the new product and use the id it comes back with. */
  const createProduct = (label: string) => {
    const value = `yeni-${label.toLocaleLowerCase('tr').replace(/\s+/g, '-')}`
    setProducts((current) => [
      ...current,
      { value, label, description: t('advancedSelectSection.createOnMissing.newRecordDescription') },
    ])
    setProduct(value)
  }

  return (
    <DemoSection
      id="gelismis-secim"
      title={t('advancedSelectSection.title')}
      description={t('advancedSelectSection.description')}
      icon={Network}
    >
      <ControlDemo
        name="<CascadingSelect/>"
        title={t('advancedSelectSection.cascading.title')}
        description={t('advancedSelectSection.cascading.description')}
        value={show(address)}
        wide
      >
        <CascadingSelect
          aria-label={t('advancedSelectSection.cascading.aria')}
          levels={ADDRESS_LEVELS}
          value={address}
          onValueChange={setAddress}
        />
      </ControlDemo>

      <ControlDemo
        name="<AsyncCombobox/>"
        title={t('advancedSelectSection.async.title')}
        description={t('advancedSelectSection.async.description')}
        value={customer ? show({ value: customer.value, label: customer.label }) : 'null'}
      >
        <AsyncCombobox
          aria-label={t('advancedSelectSection.async.aria')}
          value={customer}
          onValueChange={setCustomer}
          onSearch={searchCustomers}
        />
      </ControlDemo>

      <ControlDemo
        name="<ComboboxInput onCreate/>"
        title={t('advancedSelectSection.createOnMissing.title')}
        description={t('advancedSelectSection.createOnMissing.description')}
        value={show(product)}
      >
        <ComboboxInput
          aria-label={t('advancedSelectSection.createOnMissing.aria')}
          value={product}
          onValueChange={setProduct}
          options={products}
          onCreate={createProduct}
          placeholder={t('advancedSelectSection.createOnMissing.placeholder')}
        />
      </ControlDemo>

      <ControlDemo
        name="<TreeSelect/>"
        title={t('advancedSelectSection.tree.title')}
        description={t('advancedSelectSection.tree.description')}
        value={show(account)}
        wide
      >
        <TreeSelect
          aria-label={t('advancedSelectSection.tree.aria')}
          nodes={CHART_OF_ACCOUNTS}
          value={account}
          onValueChange={setAccount}
          placeholder={t('advancedSelectSection.tree.placeholder')}
          className="max-w-md"
        />
      </ControlDemo>

      <ControlDemo
        name="<CustomerPicker/>"
        title={t('advancedSelectSection.customerPicker.title')}
        description={t('advancedSelectSection.customerPicker.description')}
        value={customerEntity ? show({ id: customerEntity.id, display: customerEntity.display }) : 'null'}
      >
        <CustomerPicker
          aria-label={t('advancedSelectSection.customerPicker.aria')}
          value={customerEntity}
          onValueChange={setCustomerEntity}
        />
      </ControlDemo>

      <ControlDemo
        name="<ProductPicker/>"
        title={t('advancedSelectSection.productPicker.title')}
        description={t('advancedSelectSection.productPicker.description')}
        value={productEntity ? show({ id: productEntity.id, display: productEntity.display }) : 'null'}
      >
        <ProductPicker
          aria-label={t('advancedSelectSection.productPicker.aria')}
          value={productEntity}
          onValueChange={setProductEntity}
        />
      </ControlDemo>

      <ControlDemo
        name="<ErpCodeField/>"
        title={t('advancedSelectSection.erpCodeField.title')}
        description={t('advancedSelectSection.erpCodeField.description')}
        value={erpCodeEntity ? show({ id: erpCodeEntity.id, display: erpCodeEntity.display }) : 'null'}
      >
        <ErpCodeField
          aria-label={t('advancedSelectSection.erpCodeField.aria')}
          value={erpCodeEntity}
          onValueChange={setErpCodeEntity}
        />
      </ControlDemo>
    </DemoSection>
  )
}
