import { buildEntityRef } from '@/lib/entity'
import type { EntityRef } from '@/types/entity'
import { AsyncCombobox } from './AsyncCombobox'
import { searchDomainRecords, type DomainRecord } from './domainData'
import type { FieldControlProps, SelectOption } from './types'

export interface DomainEntityFieldProps extends FieldControlProps {
  value: EntityRef | null
  onValueChange: (value: EntityRef | null) => void
  placeholder?: string
  emptyMessage?: string
  errorMessage?: string
  disabled?: boolean
  className?: string
}

interface DomainEntityFieldConfig {
  /** `EntityRef.type` for every result this field returns. */
  entityType: string
  records: readonly DomainRecord[]
  /** Where `EntityRef.url` points — the demo has no per-record detail page yet. */
  url: string
}

function toOption(record: DomainRecord): SelectOption {
  return { value: record.id, label: record.display, description: record.subtitle }
}

function toEntityRef(option: SelectOption, config: DomainEntityFieldConfig): EntityRef {
  return buildEntityRef(config.entityType, option.value, option.label, config.url, option.description)
}

/**
 * Builds a picker whose value is an `EntityRef`, not a raw id — the shape
 * `Action`/`Command` contexts and any future entity-linked field expect (see
 * `src/types/entity.ts`). `CustomerPicker`, `ProductPicker` and `ErpCodeField`
 * are all this factory over a different mock catalogue; extracted because
 * three near-identical wrappers around `AsyncCombobox` is the "reused 3+
 * times" threshold, not because the abstraction was needed up front.
 */
export function createDomainEntityField(config: DomainEntityFieldConfig) {
  function DomainEntityField({ value, onValueChange, ...props }: DomainEntityFieldProps) {
    const selected = value ? toOption({ id: value.id, display: value.display, subtitle: value.subtitle }) : null

    return (
      <AsyncCombobox
        {...props}
        value={selected}
        onValueChange={(option) => onValueChange(option ? toEntityRef(option, config) : null)}
        onSearch={async (query, signal) =>
          (await searchDomainRecords(config.records, query, signal)).map(toOption)
        }
      />
    )
  }

  DomainEntityField.displayName = `DomainEntityField(${config.entityType})`
  return DomainEntityField
}
