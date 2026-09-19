import { BellOff } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { NotificationRow } from '@/features/notifications/components/NotificationRow'
import type { AppNotification } from '@/features/notifications/types'
import { formatDayHeading, groupByDay } from '@/lib/datetime'

interface NotificationListProps {
  items: AppNotification[]
  /** Day headings — worth the vertical cost in the drawer, not in the popover. */
  grouped?: boolean
  onOpen?: (item: AppNotification) => void
  onDecision?: (item: AppNotification, accepted: boolean) => void
  emptyTitle: string
  emptyDescription: string
}

/**
 * The list body, shared verbatim by the popover and the drawer. Both surfaces
 * show the same rows in the same order; only the chrome around them differs,
 * so a row marked read in one is read in the other.
 */
export function NotificationList({
  items,
  grouped = false,
  onOpen,
  onDecision,
  emptyTitle,
  emptyDescription,
}: NotificationListProps) {
  if (items.length === 0) {
    return (
      <EmptyState
        icon={BellOff}
        title={emptyTitle}
        description={emptyDescription}
        className="px-6 py-12"
      />
    )
  }

  if (!grouped) {
    return (
      <ul className="flex flex-col">
        {items.map((item) => (
          <NotificationRow key={item.id} item={item} onOpen={onOpen} onDecision={onDecision} />
        ))}
      </ul>
    )
  }

  return (
    <div className="flex flex-col">
      {groupByDay(items, (item) => item.at).map(([day, bucket]) => (
        <section key={day}>
          <h3 className="nx-eyebrow sticky top-0 z-1 bg-[var(--nx-glass)] px-4 py-2 backdrop-blur-[var(--nx-blur)]">
            {formatDayHeading(day)}
          </h3>
          <ul className="flex flex-col">
            {bucket.map((item) => (
              <NotificationRow key={item.id} item={item} onOpen={onOpen} onDecision={onDecision} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}
