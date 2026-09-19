import { Select } from '@base-ui/react/select'
import { Check, ChevronDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import {
  CONTROL_SHELL,
  POPUP,
  POPUP_GROUP_LABEL,
  POPUP_ITEM,
} from './styles'
import {
  flattenOptions,
  isOptionGroup,
  type FieldControlProps,
  type SelectOption,
  type SelectOptionGroup,
} from './types'

export interface RichSelectProps<T extends string> extends FieldControlProps {
  value: T | null
  onValueChange: (value: T) => void
  /** Flat options, or groups of them — an account plan, a product family. */
  options: readonly (SelectOption<T> | SelectOptionGroup<T>)[]
  placeholder?: string
  disabled?: boolean
  className?: string
}

/**
 * The dropdown for choices that need more than a word: an icon, a second line,
 * a heading over each family. Where a bare list of labels would do, the native
 * `Select` primitive is the cheaper and better-behaved control.
 */
export function RichSelect<T extends string>({
  value,
  onValueChange,
  options,
  placeholder,
  disabled,
  className,
  id,
  ...aria
}: RichSelectProps<T>) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('richSelect.placeholder')
  const flat = flattenOptions(options)

  return (
    <Select.Root
      items={flat}
      value={value}
      onValueChange={(next) => onValueChange(next as T)}
      disabled={disabled}
    >
      <Select.Trigger
        id={id}
        className={cn(
          CONTROL_SHELL,
          'cursor-pointer items-center justify-between gap-2 px-[13px] text-[13.5px] outline-none',
          'data-popup-open:border-ring data-popup-open:bg-[var(--nx-surface)]',
          'disabled:pointer-events-none disabled:opacity-45',
          className,
        )}
        {...aria}
      >
        <Select.Value>
          {(current: T | null) => {
            // `Value` is handed the value, not the item, so the icon and label
            // have to be looked back up in the flattened list.
            const selected = flat.find((option) => option.value === current)
            if (!selected)
              return <span className="text-[var(--nx-label-3)]">{resolvedPlaceholder}</span>
            return (
              <span className="flex min-w-0 items-center gap-2">
                {selected.icon && (
                  <selected.icon
                    aria-hidden
                    className="text-brand-graphic size-4 shrink-0"
                    strokeWidth={1.75}
                  />
                )}
                <span className="truncate">{selected.label}</span>
              </span>
            )
          }}
        </Select.Value>
        <Select.Icon className="text-muted-foreground flex shrink-0">
          <ChevronDown aria-hidden className="size-4" strokeWidth={1.7} />
        </Select.Icon>
      </Select.Trigger>

      <Select.Portal>
        <Select.Positioner
          sideOffset={6}
          alignItemWithTrigger={false}
          className="z-50 outline-none"
        >
          <Select.Popup
            className={cn(POPUP, 'max-h-[min(22rem,var(--available-height))] w-[var(--anchor-width)] overflow-y-auto')}
          >
            <Select.List>
              {options.map((entry) =>
                isOptionGroup(entry) ? (
                  <Select.Group key={entry.label}>
                    <Select.GroupLabel className={POPUP_GROUP_LABEL}>
                      {entry.label}
                    </Select.GroupLabel>
                    {entry.options.map((option) => (
                      <OptionRow key={option.value} option={option} />
                    ))}
                  </Select.Group>
                ) : (
                  <OptionRow key={entry.value} option={entry} />
                ),
              )}
            </Select.List>
          </Select.Popup>
        </Select.Positioner>
      </Select.Portal>
    </Select.Root>
  )
}

function OptionRow<T extends string>({ option }: { option: SelectOption<T> }) {
  return (
    <Select.Item value={option.value} disabled={option.disabled} className={POPUP_ITEM}>
      <Select.ItemIndicator className="col-start-1 flex">
        <Check aria-hidden className="size-4" strokeWidth={2.25} />
      </Select.ItemIndicator>
      <span className="col-start-2 flex min-w-0 flex-col">
        <Select.ItemText className="flex items-center gap-2 truncate">
          {option.icon && (
            <option.icon aria-hidden className="text-brand-graphic size-4 shrink-0" strokeWidth={1.75} />
          )}
          {option.label}
        </Select.ItemText>
        {option.description && (
          <span className="text-muted-foreground truncate text-[11.5px]">{option.description}</span>
        )}
      </span>
    </Select.Item>
  )
}
