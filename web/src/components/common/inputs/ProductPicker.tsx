import { paths } from '@/routes/paths'
import { createDomainEntityField } from './DomainEntityField'
import { MOCK_PRODUCTS } from './domainData'

/** Resolves to a product `EntityRef` via a mock server-backed search. */
export const ProductPicker = createDomainEntityField({
  entityType: 'product',
  records: MOCK_PRODUCTS,
  url: paths.demoForms,
})
