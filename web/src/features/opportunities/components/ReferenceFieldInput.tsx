import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
import type { CustomFieldDefinition } from '@/lib/custom-fields/schema'
import { isReferenceDraft, type CustomFieldDraft } from '@/lib/custom-fields/values'
import { PartyPicker } from './PartyPicker'

interface ReferenceFieldInputProps {
  definition: CustomFieldDefinition
  value: CustomFieldDraft
  onChange: (value: CustomFieldDraft) => void
  error?: string
  disabled?: boolean
}

/**
 * A `reference` field whose target is a Party (the only target v1 allows): the same server-backed picker the customer field
 * uses. A stored reference this reader cannot see is shown as unavailable and KEPT — saving an unrelated field must not drop
 * it — and can be cleared and replaced explicitly.
 */
export function ReferenceFieldInput({ definition, value, onChange, error, disabled }: ReferenceFieldInputProps) {
  const { t } = useTranslation('opportunities')
  const label = definition.isRequired ? `${definition.label} *` : definition.label
  const chosen = isReferenceDraft(value) ? value : null

  return (
    <Field label={label} error={error}>
      {(props) => chosen ? (
        <div className="flex items-center gap-2 rounded-[var(--nx-r-ctl)] border px-3 py-2 text-[13.5px]" data-testid={`reference-${definition.fieldName}`}>
          <span id={props.id} className={`min-w-0 flex-1 truncate ${chosen.accessible ? '' : 'text-muted-foreground italic'}`}>
            {chosen.accessible && chosen.label ? chosen.label : t('customFields.reference.unavailable', { id: chosen.id })}
          </span>
          <Button type="button" variant="ghost" size="icon-sm" disabled={disabled} aria-label={t('customFields.reference.clear', { name: definition.label })} onClick={() => onChange('')}>
            <X aria-hidden />
          </Button>
        </div>
      ) : (
        <PartyPicker
          {...props}
          value={null}
          onValueChange={(option) => option && onChange({ id: Number(option.value), label: option.label, accessible: true })}
        />
      )}
    </Field>
  )
}
