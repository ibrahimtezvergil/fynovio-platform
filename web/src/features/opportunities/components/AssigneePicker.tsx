import { useTranslation } from 'react-i18next'
import type { FieldControlProps, SelectOption } from '@/components/common/inputs/types'
import { useAssigneeSearcher } from '../api'
import { assigneeOption } from '../lib/referenceOptions'
import { ReferenceSearchField } from './ReferenceSearchField'

/** The new owner of an opportunity — only ever a principal the server listed as assignable for THIS record. */
export function AssigneePicker({
  opportunityId,
  value,
  onValueChange,
  ...aria
}: FieldControlProps & { opportunityId: number; value: SelectOption | null; onValueChange: (value: SelectOption | null) => void }) {
  const { t } = useTranslation('opportunities')
  const searchAssignees = useAssigneeSearcher(opportunityId)
  return (
    <ReferenceSearchField
      {...aria}
      value={value}
      onValueChange={onValueChange}
      search={async (query) => (await searchAssignees(query)).map(assigneeOption)}
      placeholder={t('reassign.assignee.placeholder')}
      emptyMessage={t('reassign.assignee.empty')}
      errorMessage={t('reassign.assignee.error')}
      forbiddenMessage={t('reassign.assignee.forbidden')}
    />
  )
}
