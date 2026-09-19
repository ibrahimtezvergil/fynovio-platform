import { CheckCircle2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/common/EmptyState'
import { Skeleton } from '@/components/ui/skeleton'
import { AttentionItemCard } from '@/features/home/components/AttentionItemCard'
import type { AttentionItem } from '@/features/home/schema'

interface AttentionSectionProps {
  items: AttentionItem[]
  isLoading?: boolean
}

/** The dashboard's dominant surface — the answer to "what should I care about right now?" */
export function AttentionSection({ items, isLoading }: AttentionSectionProps) {
  const { t } = useTranslation('home')
  const ordered = [...items].sort((a, b) => a.priority - b.priority)

  return (
    <Card className="h-full min-h-0 gap-0 rounded-[var(--nx-r-panel)] p-0">
      <div className="shrink-0 border-b border-[var(--nx-hairline)] px-5 pt-4 pb-3">
        <h2 className="font-heading text-[15.5px] leading-5 font-[620] tracking-[-0.022em]">
          {t('attention.heading')}
        </h2>
        <p className="text-muted-foreground mt-0.5 text-[12.5px]">{t('attention.description')}</p>
      </div>

      {isLoading ? (
        <div className="flex flex-col gap-3 px-5 py-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[62px] rounded-md" />
          ))}
        </div>
      ) : ordered.length === 0 ? (
        <EmptyState
          icon={CheckCircle2}
          tone="success"
          title={t('attention.emptyTitle')}
          description={t('attention.emptyDescription')}
        />
      ) : (
        // The panel's own scroll — many focus items scroll here, not the page.
        <div className="min-h-0 flex-1 overflow-y-auto">
          {ordered.map((item) => (
            <AttentionItemCard key={item.id} item={item} />
          ))}
        </div>
      )}
    </Card>
  )
}
