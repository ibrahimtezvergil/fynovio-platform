import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import type { CustomFieldDefinition } from '@/lib/custom-fields/schema'
import type { CustomFieldDraft, CustomFieldDrafts } from '@/lib/custom-fields/values'

const inputType: Partial<Record<CustomFieldDefinition['fieldType'], string>> = { email: 'email', phone: 'tel', url: 'url', date: 'date' }

interface CustomFieldInputProps {
  definition: CustomFieldDefinition
  value: CustomFieldDraft
  onChange: (value: CustomFieldDraft) => void
  error?: string
  disabled?: boolean
}

/**
 * One input for one field definition, chosen by its type. Only active options are offered; an option that was
 * deprecated but is still stored stays selected (and visible) so an unrelated edit does not silently drop it.
 */
export function CustomFieldInput({ definition, value, onChange, error, disabled }: CustomFieldInputProps) {
  const { t } = useTranslation('opportunities')
  const label = definition.isRequired ? `${definition.label} *` : definition.label
  const options = definition.config.options ?? []
  const offered = (selected: readonly string[]) => options.filter((option) => !option.isDeprecated || selected.includes(option.key))

  if (definition.fieldType === 'boolean') {
    return (
      <label className="flex items-center gap-2.5 text-[13.5px]">
        <Checkbox checked={value === true} disabled={disabled} onChange={(event) => onChange(event.target.checked)} />
        {label}
        {error && <span className="text-destructive text-[12.5px]">{error}</span>}
      </label>
    )
  }

  if (definition.fieldType === 'multi_select') {
    const selected = Array.isArray(value) ? value : []
    return (
      <Field label={label} error={error}>
        {(props) => (
          <div role="group" aria-labelledby={props['aria-labelledby']} aria-describedby={props['aria-describedby']} className="flex flex-wrap gap-x-4 gap-y-2 pt-1">
            {offered(selected).map((option) => (
              <label key={option.key} className="flex items-center gap-2 text-[13.5px]">
                <Checkbox
                  checked={selected.includes(option.key)}
                  disabled={disabled}
                  onChange={(event) => onChange(event.target.checked ? [...selected, option.key] : selected.filter((key) => key !== option.key))}
                />
                {option.label}
              </label>
            ))}
          </div>
        )}
      </Field>
    )
  }

  const text = typeof value === 'string' ? value : ''
  return (
    <Field label={label} error={error}>
      {(props) => {
        if (definition.fieldType === 'select') {
          return (
            <Select {...props} value={text} disabled={disabled} onChange={(event) => onChange(event.target.value)}>
              <option value="">{t('customFields.noValue')}</option>
              {offered(text ? [text] : []).map((option) => <option key={option.key} value={option.key}>{option.label}</option>)}
            </Select>
          )
        }
        if (definition.fieldType === 'long_text') {
          return <Textarea {...props} value={text} disabled={disabled} maxLength={definition.config.maxLength ?? 10000} onChange={(event) => onChange(event.target.value)} />
        }
        const numeric = definition.fieldType === 'number' || definition.fieldType === 'decimal'
        return (
          <Input
            {...props}
            type={inputType[definition.fieldType] ?? 'text'}
            inputMode={numeric ? (definition.fieldType === 'number' ? 'numeric' : 'decimal') : undefined}
            maxLength={definition.fieldType === 'text' ? (definition.config.maxLength ?? 2000) : undefined}
            autoComplete="off"
            value={text}
            disabled={disabled}
            onChange={(event) => onChange(event.target.value)}
          />
        )
      }}
    </Field>
  )
}

interface CustomFieldInputsProps {
  definitions: readonly CustomFieldDefinition[]
  drafts: CustomFieldDrafts
  onChange: (drafts: CustomFieldDrafts) => void
  errors?: Record<string, string>
  disabled?: boolean
}

/** The active fields as one grid of inputs — used by the create form and the detail editor alike. */
export function CustomFieldInputs({ definitions, drafts, onChange, errors, disabled }: CustomFieldInputsProps) {
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {definitions.map((definition) => (
        <div key={definition.id} className={definition.fieldType === 'long_text' || definition.fieldType === 'multi_select' ? 'sm:col-span-2' : undefined}>
          <CustomFieldInput
            definition={definition}
            value={drafts[definition.fieldName] ?? ''}
            error={errors?.[definition.fieldName]}
            disabled={disabled}
            onChange={(value) => onChange({ ...drafts, [definition.fieldName]: value })}
          />
        </div>
      ))}
    </div>
  )
}
