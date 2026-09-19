import { StageBadge } from '@/components/common/StageBadge'
import { PeekDrawer } from '@/components/common/PeekDrawer'
import { Button } from '@/components/ui/button'
import { formatCloseDate, money } from '@/features/pipeline/data/format'
import type { ReactNode } from 'react'
import type { Deal } from '@/types'

interface DealPeekDrawerProps {
  deals: Deal[]
  activeId: string | null
  onActiveIdChange: (id: string | null) => void
  onComplete: (id: string) => void
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div className="flex items-baseline justify-between gap-4 border-b border-[var(--nx-hairline-soft)] py-3 last:border-b-0"><dt className="text-muted-foreground text-[12px]">{label}</dt><dd className="text-right text-[13px] font-[550]">{children}</dd></div>
}

/** Pipeline's domain adapter for the reusable contextual peek drawer. */
export function DealPeekDrawer({ deals, activeId, onActiveIdChange, onComplete }: DealPeekDrawerProps) {
  const index = deals.findIndex((deal) => deal.id === activeId)
  const deal = index >= 0 ? deals[index] : null
  if (!deal) return null
  const move = (offset: -1 | 1) => onActiveIdChange(deals[index + offset]?.id ?? deal.id)
  return (
    <PeekDrawer
      open={activeId !== null}
      onOpenChange={(open) => { if (!open) onActiveIdChange(null) }}
      title={deal.title}
      description={`${deal.account} · ${deal.owner}`}
      position={`${index + 1} / ${deals.length}`}
      onPrevious={() => move(-1)} onNext={() => move(1)} hasPrevious={index > 0} hasNext={index < deals.length - 1}
    >
      <dl>
        <Field label="Aşama"><StageBadge stage={deal.stage} /></Field>
        <Field label="Tutar"><span className="tnum">{money.format(deal.value)}</span></Field>
        <Field label="Olasılık"><span className="tnum">%{deal.probability}</span></Field>
        <Field label="Beklenen değer"><span className="tnum">{money.format((deal.value * deal.probability) / 100)}</span></Field>
        <Field label="Tahmini kapanış">{formatCloseDate(deal.closeDate)}</Field>
      </dl>
      {deal.stage !== 'ready' && (
        <Button className="mt-5 w-full" onClick={() => {
          onComplete(deal.id)
          // Completion is a list workflow: stay in context and immediately
          // offer the next visible record instead of making the user reopen.
          onActiveIdChange(deals[index + 1]?.id ?? null)
        }}>
          Tamamlandı olarak işaretle
        </Button>
      )}
    </PeekDrawer>
  )
}
