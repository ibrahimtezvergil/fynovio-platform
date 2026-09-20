import { paths } from '@/routes/paths'
import { createDomainEntityField } from './DomainEntityField'
import { MOCK_CUSTOMERS } from './domainData'

/**
 * Resolves to a customer `EntityRef` via a mock server-backed search — no
 * customer detail page exists yet, so `EntityRef.url` points at the demo
 * route that hosts this control (mirrors `dealToEntityRef` falling back to
 * `paths.crmOpportunities` for the same reason).
 */
export const CustomerPicker = createDomainEntityField({
  entityType: 'customer',
  records: MOCK_CUSTOMERS,
  url: paths.demoForms,
})
