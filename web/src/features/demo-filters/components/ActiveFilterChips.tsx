import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import type { FilterChip } from '@/features/demo-filters/lib/query'
import type { FilterState } from '@/features/demo-filters/types'

interface ActiveFilterChipsProps {
  chips: readonly FilterChip[]
  onRemove: (patch: Partial<FilterState>) => void
  onClearAll: () => void
}

/**
 * What is currently narrowing the list, spelled out.
 *
 * This row is the panel's receipt. Without it a collapsed filter panel hides
 * its own effect, and the reader is left explaining a suspiciously short list
 * to themselves. Each chip removes exactly one predicate — a single "temizle"
 * button forces an all-or-nothing choice nobody wants.
 */
export function ActiveFilterChips({ chips, onRemove, onClearAll }: ActiveFilterChipsProps) {
  const { t } = useTranslation('demo-filters')
  if (chips.length === 0) return null

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <span className="text-[var(--nx-label-3)] mr-0.5 text-[11.5px]">{t('chips.activeLabel')}</span>
      {chips.map((chip) => (
        <button
          key={chip.key}
          type="button"
          onClick={() => onRemove(chip.clear)}
          aria-label={t('chips.removeAria', { label: chip.label })}
          className="nx-pill h-[26px] cursor-pointer bg-[var(--nx-tint-fill)] pr-2 text-[var(--nx-tint)] transition-colors hover:bg-[var(--nx-tint-fill-hover)]"
        >
          {chip.label}
          <X aria-hidden className="-mr-0.5 size-3.5" strokeWidth={2.2} />
        </button>
      ))}
      <Button variant="ghost" size="xs" onClick={onClearAll}>
        {t('chips.clearAll')}
      </Button>
    </div>
  )
}
