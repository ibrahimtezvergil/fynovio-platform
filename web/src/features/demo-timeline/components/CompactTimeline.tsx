import { useActivityMeta, type TimelineEntry } from '@/features/demo-timeline/types'
import { formatShortDate, formatTime } from '@/lib/datetime'

/**
 * The dense variant, for a detail drawer or a card that has 300px to spare.
 *
 * The tiles and the rail go; a fixed time column and a tone dot stay. What
 * survives the squeeze is what a reader scanning a history actually uses:
 * when, and what kind — the prose is one line and can be truncated.
 */
export function CompactTimeline({ entries }: { entries: readonly TimelineEntry[] }) {
  const activityMeta = useActivityMeta()
  return (
    <ol className="flex flex-col">
      {entries.map((entry) => {
        const { tone, label } = activityMeta[entry.type]
        return (
          <li
            key={entry.id}
            className="flex items-baseline gap-3 border-b border-[var(--nx-hairline-soft)] py-2.5 last:border-b-0"
          >
            <time
              dateTime={entry.at}
              className="text-muted-foreground tnum w-[86px] shrink-0 text-right text-[11px]"
            >
              {formatShortDate(entry.at)} · {formatTime(entry.at)}
            </time>
            {/* The pill class used purely as a tone lookup — fill suppressed,
                dot inherits currentColor. One tone ladder, not two. */}
            <span
              aria-hidden
              data-tone={tone}
              className="nx-pill size-[9px] shrink-0 justify-center self-center bg-transparent! p-0"
            >
              <span className="nx-pill__dot" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-[12.5px] font-[550]">
                <span className="sr-only">{label}: </span>
                {entry.title}
              </span>
              <span className="text-muted-foreground block truncate text-[11.5px]">
                {entry.actor}
                {entry.detail ? ` — ${entry.detail}` : ''}
              </span>
            </span>
          </li>
        )
      })}
    </ol>
  )
}
