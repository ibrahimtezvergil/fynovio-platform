import { Combobox } from '@base-ui/react/combobox'
import { Check, ChevronDown, X } from 'lucide-react'
import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_SHELL, POPUP, POPUP_EMPTY, POPUP_ITEM } from './styles'
import type { FieldControlProps, SelectOption } from './types'

export interface MultiSelectProps<T extends string> extends FieldControlProps {
  value: readonly T[]
  onValueChange: (value: T[]) => void
  options: readonly SelectOption<T>[]
  placeholder?: string
  emptyMessage?: string
  disabled?: boolean
  className?: string
}

/**
 * Many choices in one field, each selection a removable chip in the input.
 *
 * The chips live inside the control rather than under it so the field's height
 * tells the truth about how much has been picked — a filter bar that hides its
 * own state behind "3 seçili" is how people ship the wrong report.
 */
export function MultiSelect<T extends string>({
  value,
  onValueChange,
  options,
  placeholder,
  emptyMessage,
  disabled,
  className,
  id,
  ...aria
}: MultiSelectProps<T>) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('multiSelect.placeholder')
  const resolvedEmptyMessage = emptyMessage ?? t('multiSelect.empty')
  const selected = value
    .map((entry) => options.find((option) => option.value === entry))
    .filter((option): option is SelectOption<T> => option !== undefined)

  return (
    <Combobox.Root
      multiple
      items={options as SelectOption<T>[]}
      value={selected}
      onValueChange={(items: SelectOption<T>[]) => onValueChange(items.map((item) => item.value))}
      isItemEqualToValue={(a: SelectOption<T>, b: SelectOption<T>) => a.value === b.value}
      disabled={disabled}
    >
      <Combobox.InputGroup
        className={cn(
          CONTROL_SHELL,
          // `h-auto!` rather than `h-auto`: the shell's `h-control` uses a custom
          // spacing key, which tailwind-merge does not recognise as a height
          // utility and therefore does not drop. Both land in the class list and
          // the stylesheet's order decides — which pinned the field at one row
          // and clipped every chip past the first behind `overflow-hidden`.
          'relative h-auto! min-h-control flex-wrap items-center gap-1 py-1.5 pr-9 pl-1.5',
          className,
        )}
      >
        <Combobox.Chips className="flex w-full flex-wrap items-center gap-1">
          <Combobox.Value>
            {(items: SelectOption<T>[]) => (
              <Fragment>
                {items.map((item) => (
                  <Combobox.Chip
                    key={item.value}
                    aria-label={item.label}
                    className={cn(
                      'flex h-[26px] cursor-default items-center gap-1 rounded-sm pr-1 pl-2.5',
                      'bg-accent text-accent-foreground text-[12.5px] font-[550] outline-none',
                      'data-highlighted:bg-[var(--nx-tint-fill-hover)]',
                    )}
                  >
                    {item.label}
                    <Combobox.ChipRemove
                      aria-label={t('multiSelect.removeChip', { label: item.label })}
                      className="flex size-[18px] cursor-pointer items-center justify-center rounded-full border-0 bg-transparent p-0 text-inherit outline-none hover:bg-[var(--nx-tint-fill-hover)]"
                    >
                      <X aria-hidden className="size-3" strokeWidth={2.25} />
                    </Combobox.ChipRemove>
                  </Combobox.Chip>
                ))}
                <Combobox.Input
                  id={id}
                  placeholder={items.length > 0 ? '' : resolvedPlaceholder}
                  className="h-[26px] min-w-24 flex-1 border-0 bg-transparent px-1.5 text-[13.5px] text-foreground outline-none placeholder:text-[var(--nx-label-3)]"
                  {...aria}
                />
              </Fragment>
            )}
          </Combobox.Value>
        </Combobox.Chips>
        <Combobox.Trigger
          aria-label={t('multiSelect.openList')}
          className="text-muted-foreground hover:text-foreground absolute top-2.5 right-2.5 flex cursor-pointer border-0 bg-transparent p-0 outline-none"
        >
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
                  <Combobox.ItemIndicator className="col-start-1 flex">
                    <Check aria-hidden className="size-4" strokeWidth={2.25} />
                  </Combobox.ItemIndicator>
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
