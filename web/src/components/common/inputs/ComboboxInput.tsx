import { Combobox } from '@base-ui/react/combobox'
import { Check, ChevronDown, Plus, X } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL, POPUP, POPUP_EMPTY, POPUP_ITEM } from './styles'
import type { FieldControlProps, SelectOption } from './types'

const TAIL_BUTTON = [
  'flex w-8 shrink-0 cursor-pointer items-center justify-center border-0 bg-transparent p-0',
  'text-muted-foreground outline-none transition-colors duration-[150ms] ease-fluid hover:text-foreground',
].join(' ')

/** The synthetic row that offers to create what was typed. */
const CREATE = '__create__'

export interface ComboboxInputProps<T extends string> extends FieldControlProps {
  value: T | null
  onValueChange: (value: T | null) => void
  options: readonly SelectOption<T>[]
  /**
   * Turns the field creatable: when the query matches no option, the list
   * offers to create it. Receives the typed text; adding the option and
   * selecting it is the caller's job, because only it knows what an id costs.
   */
  onCreate?: (label: string) => void
  placeholder?: string
  emptyMessage?: string
  disabled?: boolean
  className?: string
}

/**
 * Single select with a filter. Past a couple of dozen options — customers,
 * products, cost centres — scrolling stops being a way to find anything, and
 * this is what a plain select should become.
 */
export function ComboboxInput<T extends string>({
  value,
  onValueChange,
  options,
  onCreate,
  placeholder,
  emptyMessage,
  disabled,
  className,
  id,
  ...aria
}: ComboboxInputProps<T>) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('comboboxInput.placeholder')
  const resolvedEmptyMessage = emptyMessage ?? t('comboboxInput.empty')
  const [query, setQuery] = useState('')
  const { contains } = Combobox.useFilter({ sensitivity: 'base' })
  const selected = options.find((option) => option.value === value) ?? null

  const typed = query.trim()
  const exists = options.some(
    (option) => option.label.toLocaleLowerCase('tr') === typed.toLocaleLowerCase('tr'),
  )
  const creatable = onCreate !== undefined && typed.length > 0 && !exists
  const items: SelectOption<T>[] = creatable
    ? [...options, { value: CREATE as T, label: t('comboboxInput.addAs', { value: typed }) }]
    : (options as SelectOption<T>[])

  const handleChange = (item: SelectOption<T> | null) => {
    if (item?.value === CREATE) {
      onCreate?.(typed)
      return
    }
    onValueChange(item?.value ?? null)
  }

  return (
    <Combobox.Root
      items={items}
      value={selected}
      onValueChange={handleChange}
      onInputValueChange={setQuery}
      // The create row never matches the query it is offering to create, so it
      // has to opt out of filtering rather than be filtered like an option.
      filter={(item: SelectOption<T>, search: string) =>
        item.value === CREATE || contains(item.label, search)
      }
      isItemEqualToValue={(a: SelectOption<T>, b: SelectOption<T>) => a.value === b.value}
      disabled={disabled}
    >
      <Combobox.InputGroup className={cn(CONTROL_SHELL, className)}>
        <Combobox.Input
          id={id}
          placeholder={resolvedPlaceholder}
          className={CONTROL_INPUT}
          {...aria}
        />
        {selected && (
          <Combobox.Clear className={TAIL_BUTTON} aria-label={t('comboboxInput.clearSelection')}>
            <X aria-hidden className="size-4" strokeWidth={1.75} />
          </Combobox.Clear>
        )}
        <Combobox.Trigger className={cn(TAIL_BUTTON, 'pr-2 w-8')} aria-label={t('comboboxInput.openList')}>
          <ChevronDown aria-hidden className="size-4" strokeWidth={1.7} />
        </Combobox.Trigger>
      </Combobox.InputGroup>

      <Combobox.Portal>
        <Combobox.Positioner sideOffset={6} className="z-50 outline-none">
          <Combobox.Popup className={cn(POPUP, 'w-[var(--anchor-width)]')}>
            {/* The padding lives on a child: `Empty` itself is always in the DOM as
                a live region, so styling it would leave a gap above the list. */}
            <Combobox.Empty>
              <div className={POPUP_EMPTY}>{resolvedEmptyMessage}</div>
            </Combobox.Empty>
            <Combobox.List className="max-h-[min(20rem,var(--available-height))] overflow-y-auto overscroll-contain outline-none">
              {(option: SelectOption<T>) => (
                <Combobox.Item
                  key={option.value}
                  value={option}
                  disabled={option.disabled}
                  className={POPUP_ITEM}
                >
                  {option.value === CREATE ? (
                    <Plus
                      aria-hidden
                      className="text-accent-foreground col-start-1 size-4"
                      strokeWidth={2}
                    />
                  ) : (
                    <Combobox.ItemIndicator className="col-start-1 flex">
                      <Check aria-hidden className="size-4" strokeWidth={2.25} />
                    </Combobox.ItemIndicator>
                  )}
                  <span className="col-start-2 flex min-w-0 flex-col">
                    <span className="truncate">{option.label}</span>
                    {option.description && (
                      <span className="text-muted-foreground truncate text-[11.5px]">
                        {option.description}
                      </span>
                    )}
                  </span>
                </Combobox.Item>
              )}
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  )
}
