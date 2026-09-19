import { Users } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/common/EmptyState'
import { Skeleton } from '@/components/ui/skeleton'
import { TeamActivityRow } from '@/features/home/components/TeamActivityRow'
import type { TeamActivityItem } from '@/features/home/schema'

interface TeamActivitySectionProps {
  items: TeamActivityItem[]
  isLoading?: boolean
}

/** The product's multiplayer feel — light presence, not a Slack-style feed. */
export function TeamActivitySection({ items, isLoading }: TeamActivitySectionProps) {
  const { t } = useTranslation('home')

  return (
    <Card className="h-full min-h-0 gap-2.5 overflow-hidden px-4 pt-3.5 pb-3.5">
      <h2 className="font-heading shrink-0 px-1 text-[13px] leading-5 font-[620] tracking-[-0.02em]">
        {t('teamActivity.heading')}
      </h2>
      {isLoading ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[40px] rounded-md" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={Users}
          title={t('teamActivity.emptyTitle')}
          description={t('teamActivity.emptyDescription')}
          className="py-6"
        />
      ) : (
        <div className="flex min-h-0 flex-1 flex-col gap-0.5 overflow-y-auto">
          {items.map((item) => (
            <TeamActivityRow key={item.id} item={item} />
          ))}
        </div>
      )}
    </Card>
  )
}
