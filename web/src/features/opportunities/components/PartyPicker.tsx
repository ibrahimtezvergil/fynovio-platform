import { useTranslation } from 'react-i18next'
import type { FieldControlProps, SelectOption } from '@/components/common/inputs/types'
import { usePartySearcher } from '../api'
import { partyOption } from '../lib/referenceOptions'
import { ReferenceSearchField } from './ReferenceSearchField'

/** Choose the customer of a new opportunity from the tenant's Parties. The value is the Party id the backend will verify. */
export function PartyPicker({
  value,
  onValueChange,
  ...aria
}: FieldControlProps & { value: SelectOption | null; onValueChange: (value: SelectOption | null) => void }) {
  const { t } = useTranslation('opportunities')
  const searchParties = usePartySearcher()
  return (
    <ReferenceSearchField
      {...aria}
      value={value}
      onValueChange={onValueChange}
      search={async (query) => (await searchParties(query)).map(partyOption)}
      placeholder={t('form.partyId.placeholder')}
      emptyMessage={t('picker.empty')}
      errorMessage={t('picker.error')}
      forbiddenMessage={t('picker.forbidden')}
    />
  )
}
