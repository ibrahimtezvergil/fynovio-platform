import { paths } from '@/routes/paths'
import { createDomainEntityField } from './DomainEntityField'
import { MOCK_ERP_CODES } from './domainData'

/** Resolves to a chart-of-accounts `EntityRef` via a mock server-backed search. */
export const ErpCodeField = createDomainEntityField({
  entityType: 'erpCode',
  records: MOCK_ERP_CODES,
  url: paths.demoForms,
})
