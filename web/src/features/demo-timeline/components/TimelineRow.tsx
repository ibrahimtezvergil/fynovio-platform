import { Paperclip, Pin, Timer } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ObjectDiff } from '@/components/common/ObjectDiff'
import { formatTime, relativeTime } from '@/lib/datetime'
import { useActivityMeta, type TimelineEntry } from '@/features/demo-timeline/types'

/**
 * One event on the rail.
 *
 * The tile's tone repeats the type, which is also written out in the meta
 * line — a feed read in greyscale still says whether something was a call or
 * an e-mail. The rail itself is `aria-hidden`: it is a drawn line, and the
 * `<ol>` already tells a screen reader these entries are ordered.
 */
export function TimelineRow({ entry }: { entry: TimelineEntry }) {
  const { t } = useTranslation('demo-timeline')
  const activityMeta = useActivityMeta()
  const { icon: Icon, tone, label } = activityMeta[entry.type]

  return (
    <li className="group/entry relative flex gap-3 pb-5 last:pb-0">
      <span
        aria-hidden
        className="absolute top-[34px] bottom-0 left-[15px] w-px bg-[var(--nx-hairline)] group-last/entry:hidden"
      />

      <span aria-hidden data-tone={tone} className="nx-icon-tile relative z-1">
        <Icon className="size-4" strokeWidth={1.75} />
      </span>

      <div className="flex min-w-0 flex-1 flex-col gap-1 pt-[5px]">
        <div className="flex items-start gap-2">
          <p className="min-w-0 flex-1 text-[13px] leading-[1.35] font-[590] tracking-[-0.012em]">
            {entry.pinned && (
              <Pin aria-hidden className="mr-1 inline size-3 align-[-1px]" strokeWidth={2} />
            )}
            {entry.title}
          </p>
          <time
            dateTime={entry.at}
            title={formatTime(entry.at)}
            className="text-muted-foreground tnum mt-px shrink-0 text-[11px]"
          >
            {relativeTime(entry.at)}
          </time>
        </div>

        <p className="text-[var(--nx-label-3)] flex flex-wrap items-center gap-x-1.5 text-[11.5px]">
          <span className="text-muted-foreground font-[550]">{entry.actor}</span>
          <span aria-hidden>·</span>
          <span>{label}</span>
          {entry.direction && (
            <>
              <span aria-hidden>·</span>
              <span>{t(`direction.${entry.direction}`)}</span>
            </>
          )}
          {entry.duration && (
            <>
              <span aria-hidden>·</span>
              <span className="inline-flex items-center gap-1">
                <Timer aria-hidden className="size-3" strokeWidth={1.8} />
                {entry.duration}
              </span>
            </>
          )}
        </p>

        {entry.detail && (
          <p className="text-muted-foreground text-[12.5px] leading-[1.5]">{entry.detail}</p>
        )}

        {entry.change && (
          <div className="mt-0.5"><ObjectDiff before={{ [entry.change.field]: entry.change.from }} after={{ [entry.change.field]: entry.change.to }} /></div>
        )}

        {entry.attachments && entry.attachments.length > 0 && (
          <ul className="mt-0.5 flex flex-wrap gap-1.5">
            {entry.attachments.map((file) => (
              <li key={file}>
                <a
                  href="#"
                  onClick={(event) => event.preventDefault()}
                  className="text-muted-foreground inline-flex items-center gap-1.5 rounded-sm border border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-2 py-1 text-[11px] transition-colors hover:border-[var(--nx-hairline-strong)] hover:text-foreground"
                >
                  <Paperclip aria-hidden className="size-3" strokeWidth={1.8} />
                  {file}
                </a>
              </li>
            ))}
          </ul>
        )}
      </div>
    </li>
  )
}
