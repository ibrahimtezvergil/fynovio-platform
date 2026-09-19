import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import {
  CheckboxGroupField,
  DateRangePicker,
  MultiSelect,
  NumberRangeInput,
} from '@/components/common/inputs'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import { CITIES, OWNERS } from '@/features/demo-filters/data/records'
import {
  CHANNELS,
  ORDER_STATUSES,
  useChannelLabels,
  useOrderStatusMeta,
  type Channel,
  type FilterState,
  type OrderStatus,
} from '@/features/demo-filters/types'

interface FilterPanelProps {
  id: string
  filter: FilterState
  onPatch: (patch: Partial<FilterState>) => void
  onClear: () => void
}

/**
 * The expandable panel: everything that does not fit — and does not belong —
 * in the always-visible bar.
 *
 * Two or three filters carry almost all the traffic, and they stay on the bar
 * as pills. The rest live here, collapsed by default, because a permanently
 * open eight-field panel costs every user vertical space to serve the few who
 * need the seventh field.
 */
export function FilterPanel({ id, filter, onPatch, onClear }: FilterPanelProps) {
  const { t } = useTranslation('demo-filters')
  const orderStatus = useOrderStatusMeta()
  const channelLabels = useChannelLabels()

  const statusOptions = ORDER_STATUSES.map((value) => ({ value, label: orderStatus[value].label }))
  const ownerOptions = OWNERS.map((value) => ({ value, label: value }))
  const cityOptions = CITIES.map((value) => ({ value, label: value }))
  const channelOptions = CHANNELS.map((value) => ({ value, label: channelLabels[value] }))

  return (
    <div
      id={id}
      className="flex flex-col gap-4 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4"
    >
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Field label={t('panel.statusLabel')} hint={t('panel.statusHint')}>
          {(props) => (
            <MultiSelect
              {...props}
              options={statusOptions}
              value={filter.statuses}
              onValueChange={(value) => onPatch({ statuses: value as OrderStatus[] })}
              placeholder={t('panel.statusPlaceholder')}
            />
          )}
        </Field>

        <Field label={t('panel.ownerLabel')}>
          {(props) => (
            <MultiSelect
              {...props}
              options={ownerOptions}
              value={filter.owners}
              onValueChange={(value) => onPatch({ owners: value })}
              placeholder={t('panel.ownerPlaceholder')}
            />
          )}
        </Field>

        <Field label={t('panel.cityLabel')}>
          {(props) => (
            <MultiSelect
              {...props}
              options={cityOptions}
              value={filter.cities}
              onValueChange={(value) => onPatch({ cities: value })}
              placeholder={t('panel.cityPlaceholder')}
            />
          )}
        </Field>

        <Field label={t('panel.amountLabel')} hint={t('panel.amountHint')}>
          {(props) => (
            <NumberRangeInput
              {...props}
              value={filter.amount}
              onValueChange={(value) => onPatch({ amount: value })}
            />
          )}
        </Field>

        <Field label={t('panel.dateLabel')}>
          {(props) => (
            <DateRangePicker
              {...props}
              value={filter.dates}
              onValueChange={(value) => onPatch({ dates: value })}
            />
          )}
        </Field>

        <Field label={t('panel.channelLabel')}>
          {(props) => (
            <CheckboxGroupField
              {...props}
              options={channelOptions}
              value={filter.channels}
              onValueChange={(value) => onPatch({ channels: value as Channel[] })}
            />
          )}
        </Field>
      </div>

      <Separator />

      <div className="flex flex-wrap items-center gap-3">
        <label
          htmlFor="only-tagged"
          className="flex cursor-pointer items-center gap-2.5 text-[12.5px] font-[550]"
        >
          <Switch
            id="only-tagged"
            checked={filter.onlyTagged}
            onCheckedChange={(checked) => onPatch({ onlyTagged: checked })}
          />
          {t('panel.onlyTagged')}
        </label>
        <div className="flex-1" />
        <Button variant="ghost" size="sm" onClick={onClear}>
          {t('panel.clearFilters')}
        </Button>
      </div>
    </div>
  )
}
