import { Check } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useActivityMeta, type ActivityType } from '@/features/demo-timeline/types'
import { cn } from '@/lib/utils'

interface FeedFilterProps {
  /** Only the types this feed actually contains — an empty filter is noise. */
  available: readonly ActivityType[]
  /** Empty means "everything"; a filter that starts fully ticked reads as broken. */
  value: readonly ActivityType[]
  onChange: (next: ActivityType[]) => void
}

/**
 * Type filter as toggle pills rather than a dropdown: with fewer than about
 * eight options, a menu hides both the choices and the current answer behind
 * a click, and the answer is the thing the reader most needs to see.
 */
export function FeedFilter({ available, value, onChange }: FeedFilterProps) {
  const { t } = useTranslation('demo-timeline')
  const activityMeta = useActivityMeta()
  const toggle = (type: ActivityType) =>
    onChange(value.includes(type) ? value.filter((entry) => entry !== type) : [...value, type])

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <button
        type="button"
        aria-pressed={value.length === 0}
        onClick={() => onChange([])}
        className={cn(
          'nx-pill h-[26px] cursor-pointer transition-colors',
          value.length === 0
            ? 'bg-[var(--nx-tint-fill)] text-[var(--nx-tint)]'
            : 'text-muted-foreground bg-transparent shadow-[inset_0_0_0_1px_var(--nx-hairline-strong)]',
        )}
      >
        {t('feedFilter.all')}
      </button>

      {available.map((type) => {
        const { label, icon: Icon, tone } = activityMeta[type]
        const active = value.includes(type)
        return (
          <button
            key={type}
            type="button"
            aria-pressed={active}
            data-tone={active ? tone : undefined}
            onClick={() => toggle(type)}
            className={cn(
              'nx-pill h-[26px] cursor-pointer transition-colors',
              !active &&
                'text-muted-foreground bg-transparent shadow-[inset_0_0_0_1px_var(--nx-hairline-strong)]',
            )}
          >
            {active ? (
              <Check aria-hidden className="-ml-0.5 size-3.5" strokeWidth={2.4} />
            ) : (
              <Icon aria-hidden className="-ml-0.5 size-3.5" strokeWidth={1.8} />
            )}
            {label}
          </button>
        )
      })}
    </div>
  )
}
