import { Eye, EyeOff } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

/** Test predicates only — the label a rule shows lives in the `common` catalog, keyed by index. */
const RULES = [
  (value: string) => value.length >= 12,
  (value: string) => /\d/.test(value),
  (value: string) => /[a-zçğıöşü]/.test(value) && /[A-ZÇĞİÖŞÜ]/.test(value),
  (value: string) => /[^\p{L}\d]/u.test(value),
] as const

const STRENGTH_TONES = [
  'bg-[var(--nx-neg)]',
  'bg-[var(--nx-neg)]',
  'bg-[var(--nx-st-amber-fg)]',
  'bg-[var(--nx-st-teal-fg)]',
  'bg-[var(--nx-pos)]',
] as const

/** How many of the four rules the value satisfies — a count, not a score. */
export function passwordStrength(value: string): number {
  return RULES.filter((test) => test(value)).length
}

export interface PasswordInputProps extends FieldControlProps {
  value: string
  onValueChange: (value: string) => void
  autoComplete?: string
  placeholder?: string
  /** The four-segment meter and the missing-rule list. Off for a login field. */
  showStrength?: boolean
  disabled?: boolean
  className?: string
}

/**
 * Password with a reveal toggle and, where a password is being *set*, a meter.
 * The meter counts satisfied rules and names the ones still missing, which is
 * the only feedback a user can act on.
 */
export function PasswordInput({
  value,
  onValueChange,
  autoComplete = 'current-password',
  placeholder,
  showStrength = false,
  disabled,
  className,
  id,
  ...aria
}: PasswordInputProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('passwordInput.placeholder')
  const ruleLabels = t('passwordInput.rules', { returnObjects: true }) as string[]
  const strengthLabels = t('passwordInput.strength', { returnObjects: true }) as string[]
  const [revealed, setRevealed] = useState(false)
  const score = passwordStrength(value)
  const missing = RULES.map((test, index) => (test(value) ? null : ruleLabels[index])).filter(
    (label): label is string => label !== null,
  )

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div className={CONTROL_SHELL}>
        <input
          id={id}
          type={revealed ? 'text' : 'password'}
          value={value}
          autoComplete={autoComplete}
          placeholder={resolvedPlaceholder}
          disabled={disabled}
          onChange={(event) => onValueChange(event.target.value)}
          className={CONTROL_INPUT}
          {...aria}
        />
        <button
          type="button"
          aria-label={revealed ? t('passwordInput.hide') : t('passwordInput.show')}
          aria-pressed={revealed}
          onClick={() => setRevealed((current) => !current)}
          className="text-muted-foreground hover:text-foreground flex shrink-0 cursor-pointer items-center border-0 bg-transparent px-3 outline-none"
        >
          {revealed ? (
            <EyeOff aria-hidden className="size-4" strokeWidth={1.75} />
          ) : (
            <Eye aria-hidden className="size-4" strokeWidth={1.75} />
          )}
        </button>
      </div>

      {showStrength && (
        <div className="flex flex-col gap-1.5">
          <div className="flex gap-1" aria-hidden>
            {RULES.map((_test, index) => (
              <span
                key={index}
                className={cn(
                  'h-1 flex-1 rounded-full transition-colors duration-[250ms] ease-fluid',
                  index < score ? STRENGTH_TONES[score] : 'bg-[var(--nx-track)]',
                )}
              />
            ))}
          </div>
          <p className="text-muted-foreground text-[11.5px]" aria-live="polite">
            <span className="text-foreground font-[550]">{strengthLabels[score]}</span>
            {missing.length > 0 && ` · ${t('passwordInput.missing', { rules: missing.join(', ') })}`}
          </p>
        </div>
      )}
    </div>
  )
}
