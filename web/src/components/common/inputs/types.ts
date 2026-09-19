import type { LucideIcon } from 'lucide-react'

/**
 * What `Field`'s render prop hands a control. Every input in this kit accepts
 * these, so `<Field>{(props) => <AnyInput {...props} />}</Field>` always works.
 */
export interface FieldControlProps {
  id?: string
  /** Names group-shaped controls, where a label's `htmlFor` resolves to nothing. */
  'aria-labelledby'?: string
  'aria-describedby'?: string
  'aria-invalid'?: boolean
  'aria-label'?: string
}

/** One choice in a select, combobox, radio group or multi-select. */
export interface SelectOption<T extends string = string> {
  value: T
  label: string
  /** Second line under the label — what distinguishes two similar choices. */
  description?: string
  icon?: LucideIcon
  disabled?: boolean
}

export interface SelectOptionGroup<T extends string = string> {
  label: string
  options: SelectOption<T>[]
}

export function isOptionGroup<T extends string>(
  entry: SelectOption<T> | SelectOptionGroup<T>,
): entry is SelectOptionGroup<T> {
  return 'options' in entry
}

/** Flattens a possibly-grouped list — what `Value` needs to resolve a label. */
export function flattenOptions<T extends string>(
  entries: readonly (SelectOption<T> | SelectOptionGroup<T>)[],
): SelectOption<T>[] {
  return entries.flatMap((entry) => (isOptionGroup(entry) ? entry.options : [entry]))
}
