import { ChevronRight } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { StatusBadge, type StatusTone } from '@/components/common/StatusBadge'
import type { AttentionItem, AttentionSeverity } from '@/features/home/schema'

const SEVERITY_TONE: Record<AttentionSeverity, StatusTone> = { critical: 'red', warning: 'amber' }

const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})

/**
 * Linear-issue-style row, not a KPI tile — severity, title, entity, one
 * primary action. Renders exactly the item it is given; no business rule
 * (what counts as critical, what gets surfaced) lives here.
 */
export function AttentionItemCard({ item }: { item: AttentionItem }) {
  const { t } = useTranslation('home')
  const navigate = useNavigate()

  const metaLine = [
    [item.entityLabel, item.documentNumber].filter(Boolean).join(' · '),
    item.description,
    item.amount != null ? money.format(item.amount) : null,
    item.timestamp,
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <div className="flex items-center gap-3 border-b border-[var(--nx-hairline)] px-5 py-2.5 last:border-b-0">
      <StatusBadge label={t(`attention.severity.${item.severity}`)} tone={SEVERITY_TONE[item.severity]} className="shrink-0" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[13px] leading-5 font-[590]">{item.title}</p>
        <p className="text-muted-foreground truncate text-[11.5px] leading-4">{metaLine}</p>
      </div>
      {item.action && (
        <button
          type="button"
          onClick={() => navigate(item.action?.url ?? '/')}
          className="flex shrink-0 cursor-pointer items-center gap-0.5 text-[12.5px] font-[590] text-[var(--nx-tint)] hover:underline"
        >
          {item.action.label}
          <ChevronRight aria-hidden className="size-3.5" strokeWidth={2} />
        </button>
      )}
    </div>
  )
}
