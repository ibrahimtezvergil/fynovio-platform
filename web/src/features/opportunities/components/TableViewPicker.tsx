import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Select } from '@/components/ui/select'
import type { SharedView } from '@/lib/shared-views/schema'

interface TableViewPickerProps {
  views: readonly SharedView[]
  /** The selected view's key; null is the default, every column. */
  value: string | null
  onChange: (key: string | null) => void
}

/**
 * Which shared table view the list shows. Offered only when the tenant has any active view. A key in the URL that no longer names
 * an active view (retired since the link was shared) is shown as the default rather than as an error: the list still works.
 */
export function TableViewPicker({ views, value, onChange }: TableViewPickerProps) {
  const { t } = useTranslation('opportunities')
  const active = views.filter((view) => view.status === 'Active')
  if (active.length === 0) return null
  const selected = active.some((view) => view.key === value) ? value : null

  return (
    <Field label={t('views.picker.label')} className="w-56">
      {(props) => (
        <Select {...props} value={selected ?? ''} onChange={(event) => onChange(event.target.value || null)}>
          <option value="">{t('views.picker.all')}</option>
          {active.map((view) => <option key={view.id} value={view.key}>{view.name}</option>)}
        </Select>
      )}
    </Field>
  )
}
