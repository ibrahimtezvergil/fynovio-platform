import { Rows2, Rows4 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { useDensity } from '@/store/useAppStore'
import type { Density } from '@/types'

interface DensityToggleProps {
  /** Drops the visible labels for narrow toolbars; they stay in the a11y tree. */
  iconOnly?: boolean
}

/**
 * The density switch: two segments over one boolean.
 *
 * It writes the global preference and nothing else. The visual change is the
 * `data-density` attribute the store puts on `<html>` meeting a `.nx-dense`
 * region further down the tree — so flipping this re-renders the toggle, not
 * a single table row.
 */
export function DensityToggle({ iconOnly = false }: DensityToggleProps) {
  const { t } = useTranslation('common')
  const { density, setCompact } = useDensity()

  const segments: readonly Segment<Density>[] = [
    { value: 'comfortable', label: t('densityToggle.comfortable'), icon: Rows2 },
    { value: 'compact', label: t('densityToggle.compact'), icon: Rows4 },
  ]

  return (
    <SegmentedControl
      aria-label={t('densityToggle.ariaLabel')}
      segments={segments}
      value={density}
      iconOnly={iconOnly}
      onChange={(next) => setCompact(next === 'compact')}
    />
  )
}
