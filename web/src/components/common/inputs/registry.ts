import type { ComponentType } from 'react'
import { CustomerPicker } from './CustomerPicker'
import type { DomainEntityFieldProps } from './DomainEntityField'
import { ErpCodeField } from './ErpCodeField'
import { ProductPicker } from './ProductPicker'

/**
 * The domain-tier field kinds a type→component resolver can dispatch on.
 * Nothing calls `fieldRegistry` yet — #21 (Schema-Driven Forms) is
 * deliberately not being built now (see `ENTERPRISE_LAYERS_ASSESSMENT.md`),
 * but if it is, its resolver has a registry to read instead of inventing one.
 */
export type FieldKind = 'customer' | 'product' | 'erpCode'

export const fieldRegistry: Record<FieldKind, ComponentType<DomainEntityFieldProps>> = {
  customer: CustomerPicker,
  product: ProductPicker,
  erpCode: ErpCodeField,
}
