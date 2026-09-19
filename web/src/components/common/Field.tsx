import { useId, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface FieldProps {
  label: string
  hint?: string
  /** Renders the hint in the negative tone and marks the control invalid. */
  error?: string
  className?: string
  /** Receives the ids that wire the label, the hint and the invalid state. */
  children: (props: {
    id: string
    'aria-labelledby': string
    'aria-describedby'?: string
    'aria-invalid'?: true
  }) => ReactNode
}

/**
 * Label · control · hint, in one stack. The control is a render prop so the
 * field can hand it the ids without cloning elements or guessing its shape.
 *
 * The label is wired twice on purpose. `htmlFor` is what makes the label
 * clickable, but it only resolves to a labelable element — a control that is a
 * *group* (a radio row, a cascading address, an allocation table) renders a
 * `<div role="group">`, and `htmlFor` pointing at a div names nothing. So the
 * label also carries an id and every control is handed `aria-labelledby`,
 * which is the one mechanism both shapes honour. Where both apply they name
 * the same text, so the announced name does not change.
 */
export function Field({ label, hint, error, className, children }: FieldProps) {
  const id = useId()
  const labelId = `${id}-label`
  const describedBy = error || hint ? `${id}-hint` : undefined

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <label htmlFor={id} id={labelId} className="text-muted-foreground text-[12.5px] font-[550]">
        {label}
      </label>
      {children({
        id,
        'aria-labelledby': labelId,
        'aria-describedby': describedBy,
        ...(error ? { 'aria-invalid': true as const } : {}),
      })}
      {(error || hint) && (
        <span
          id={describedBy}
          role={error ? 'alert' : undefined}
          className={cn('text-[11.5px]', error ? 'text-[var(--nx-neg)]' : 'text-muted-foreground')}
        >
          {error ?? hint}
        </span>
      )}
    </div>
  )
}
