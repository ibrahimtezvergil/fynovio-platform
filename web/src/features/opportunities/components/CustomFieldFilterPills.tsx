import { ChevronDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { filterableFields, MAX_CUSTOM_FIELD_FILTERS } from '@/lib/custom-fields/values'
import type { CustomFieldDefinition } from '@/lib/custom-fields/schema'

const ALL = '__all__'

interface CustomFieldFilterPillsProps {
  definitions: readonly CustomFieldDefinition[]
  value: Readonly<Record<string, string>>
  onChange: (next: Record<string, string>) => void
  pillProps: (active: boolean) => { variant: 'tinted' | 'secondary'; size: 'sm'; className: string }
}

/**
 * One pill per active select, multi-select or boolean field. Unlike the search and owner pills these filter on the
 * server, so they narrow every page, not only the loaded one. The API takes at most five at once.
 */
export function CustomFieldFilterPills({ definitions, value, onChange, pillProps }: CustomFieldFilterPillsProps) {
  const { t } = useTranslation('opportunities')
  const atLimit = Object.keys(value).length >= MAX_CUSTOM_FIELD_FILTERS

  return filterableFields(definitions).map((definition) => {
    const selected = value[definition.fieldName]
    const options =
      definition.fieldType === 'boolean'
        ? [{ key: 'true', label: t('customFields.yes') }, { key: 'false', label: t('customFields.no') }]
        : (definition.config.options ?? []).filter((option) => !option.isDeprecated || option.key === selected)
    const selectedLabel = options.find((option) => option.key === selected)?.label

    const choose = (next: string) => {
      const { [definition.fieldName]: _removed, ...rest } = value
      onChange(next === ALL ? rest : { ...rest, [definition.fieldName]: next })
    }

    return (
      <DropdownMenu key={definition.fieldName}>
        <DropdownMenuTrigger
          disabled={atLimit && selected === undefined}
          render={
            <Button {...pillProps(selected !== undefined)}>
              {definition.label}: {selectedLabel ?? t('list.filters.all')}
              <ChevronDown aria-hidden strokeWidth={1.7} />
            </Button>
          }
        />
        <DropdownMenuContent className="w-56">
          <DropdownMenuRadioGroup value={selected ?? ALL} onValueChange={choose}>
            <DropdownMenuRadioItem value={ALL}>{t('list.filters.all')}</DropdownMenuRadioItem>
            <DropdownMenuSeparator />
            {options.map((option) => (
              <DropdownMenuRadioItem key={option.key} value={option.key}>
                {option.label}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuContent>
      </DropdownMenu>
    )
  })
}
