import { Inbox } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { EmptyState } from '@/components/common/EmptyState'
import { TimelineRow } from '@/features/demo-timeline/components/TimelineRow'
import type { TimelineEntry } from '@/features/demo-timeline/types'
import { formatDayHeading, groupByDay } from '@/lib/datetime'

interface TimelineFeedProps {
  entries: readonly TimelineEntry[]
  /** Day headings. Worth the vertical cost once a feed spans more than a day. */
  grouped?: boolean
  /** Pinned entries lifted above the chronology, under their own heading. */
  showPinned?: boolean
  emptyDescription?: string
}

/**
 * The feed. Newest first, always — a history read top-down is a history the
 * reader has to scroll to the bottom of before they learn what just happened.
 *
 * Pinned entries break chronology on purpose and say so with a heading, so
 * the exception never looks like the feed being out of order.
 */
export function TimelineFeed({
  entries,
  grouped = true,
  showPinned = true,
  emptyDescription,
}: TimelineFeedProps) {
  const { t } = useTranslation('demo-timeline')
  const pinned = showPinned ? entries.filter((entry) => entry.pinned) : []
  const rest = showPinned ? entries.filter((entry) => !entry.pinned) : [...entries]

  if (entries.length === 0) {
    return (
      <EmptyState
        icon={Inbox}
        title={t('feed.emptyTitle')}
        description={emptyDescription ?? t('feed.defaultEmptyDescription')}
        className="py-12"
      />
    )
  }

  return (
    <div className="flex flex-col gap-4">
      {pinned.length > 0 && (
        <section>
          <h3 className="nx-eyebrow mb-2.5">{t('feed.pinnedHeading')}</h3>
          <ol className="flex flex-col">
            {pinned.map((entry) => (
              <TimelineRow key={entry.id} entry={entry} />
            ))}
          </ol>
        </section>
      )}

      {grouped ? (
        groupByDay(rest, (entry) => entry.at).map(([day, bucket]) => (
          <section key={day}>
            <h3 className="nx-eyebrow mb-2.5">{formatDayHeading(day)}</h3>
            <ol className="flex flex-col">
              {bucket.map((entry) => (
                <TimelineRow key={entry.id} entry={entry} />
              ))}
            </ol>
          </section>
        ))
      ) : (
        <ol className="flex flex-col">
          {rest.map((entry) => (
            <TimelineRow key={entry.id} entry={entry} />
          ))}
        </ol>
      )}
    </div>
  )
}
