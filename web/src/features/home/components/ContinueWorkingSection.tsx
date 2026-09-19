import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/common/EmptyState'
import { Skeleton } from '@/components/ui/skeleton'
import { History } from 'lucide-react'
import { RecentWorkRow } from '@/features/home/components/RecentWorkRow'
import type { RecentWorkItem } from '@/features/home/schema'

interface ContinueWorkingSectionProps {
  items: RecentWorkItem[]
  isLoading?: boolean
}

/** One click back to what you were just doing. Compact object list, not a dense table. */
export function ContinueWorkingSection({ items, isLoading }: ContinueWorkingSectionProps) {
  const { t } = useTranslation('home')

  return (
    <Card className="h-full min-h-0 gap-2.5 overflow-hidden px-4 pt-3.5 pb-3.5">
      <h2 className="font-heading shrink-0 px-1 text-[13px] leading-5 font-[620] tracking-[-0.02em]">
        {t('continueWorking.heading')}
      </h2>
      {isLoading ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[40px] rounded-md" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={History}
          title={t('continueWorking.emptyTitle')}
          description={t('continueWorking.emptyDescription')}
          className="py-6"
        />
      ) : (
        <div className="flex min-h-0 flex-1 flex-col gap-0.5 overflow-y-auto">
          {items.map((item) => (
            <RecentWorkRow key={item.id} item={item} />
          ))}
        </div>
      )}
    </Card>
  )
}
