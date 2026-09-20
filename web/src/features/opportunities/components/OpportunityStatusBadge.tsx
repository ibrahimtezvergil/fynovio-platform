import { useTranslation } from 'react-i18next'
import { StatusBadge, type StatusTone } from '@/components/common/StatusBadge'
import type { OpportunityStatus } from '../schema'

/** The one place a lifecycle status gets its colour — the same hue must never mean two things (`StatusBadge`). */
const TONE: Record<OpportunityStatus, StatusTone> = { Draft: 'gray', Open: 'blue', Won: 'green', Lost: 'red' }

export function OpportunityStatusBadge({ status, size }: { status: OpportunityStatus; size?: 'sm' | 'md' }) {
  const { t } = useTranslation('opportunities')
  return <StatusBadge label={t(`status.${status}`)} tone={TONE[status]} size={size} />
}
