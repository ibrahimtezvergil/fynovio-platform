import { Check, Undo2, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { relativeTime } from '@/lib/datetime'
import { useKindMeta, type AppNotification } from '@/features/notifications/types'
import { useNotificationStore } from '@/features/notifications/store/useNotificationStore'
import { cn } from '@/lib/utils'

interface NotificationRowProps {
  item: AppNotification
  /** Opening a notification is what marks it read — the caller decides where it goes. */
  onOpen?: (item: AppNotification) => void
  onDecision?: (item: AppNotification, accepted: boolean) => void
}

/**
 * One notification. The whole row is the target, painted by a stretched
 * pseudo-element on the title button rather than by wrapping everything in a
 * `<button>` — the dismiss control and the decision buttons are real buttons
 * and cannot nest inside another one.
 *
 * The hover controls are positioned out of flow on purpose: kept in the flex
 * row they would reserve ~60px of width from every title, in a panel that is
 * only 392px wide to begin with. They are the rarely-used half of the row and
 * shouldn't tax the half that is read every time.
 *
 * Unread is carried three ways: a tinted ground, a heavier title and a dot
 * with a screen-reader label. Never the tint alone.
 */
export function NotificationRow({ item, onOpen, onDecision }: NotificationRowProps) {
  const { t } = useTranslation('notifications')
  const { icon: Icon, tone, label } = useKindMeta()[item.kind]
  const markRead = useNotificationStore((state) => state.markRead)
  const markUnread = useNotificationStore((state) => state.markUnread)
  const dismiss = useNotificationStore((state) => state.dismiss)

  return (
    <li
      data-unread={item.read ? undefined : ''}
      className="group/notif relative flex gap-3 border-b border-[var(--nx-hairline-soft)] px-4 py-3 transition-colors last:border-b-0 hover:bg-[var(--nx-fill-hover)] data-unread:bg-[var(--nx-tint-fill)]"
    >
      <span aria-hidden data-tone={tone} className="nx-icon-tile mt-0.5">
        <Icon className="size-4" strokeWidth={1.75} />
      </span>

      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex items-start gap-2">
          <button
            type="button"
            onClick={() => {
              markRead(item.id)
              onOpen?.(item)
            }}
            className="min-w-0 flex-1 text-left after:absolute after:inset-0 after:content-['']"
          >
            <span className="sr-only">{label}: </span>
            <span
              className={cn(
                'line-clamp-2 text-[13px] leading-[1.35] tracking-[-0.012em]',
                item.read ? 'font-[550]' : 'font-[650]',
              )}
            >
              {item.title}
            </span>
          </button>
          {!item.read && (
            <span className="mt-1 shrink-0">
              <span aria-hidden className="block size-[7px] rounded-full bg-[var(--nx-tint)]" />
              <span className="sr-only">{t('row.unread')}</span>
            </span>
          )}
        </div>

        <p className="text-muted-foreground line-clamp-2 text-[12px] leading-[1.5]">{item.body}</p>

        <p className="text-[var(--nx-label-3)] flex flex-wrap items-center gap-x-1.5 text-[11px]">
          {item.actor && (
            <>
              <span>{item.actor}</span>
              <span aria-hidden>·</span>
            </>
          )}
          <time dateTime={item.at} className="tnum">
            {relativeTime(item.at)}
          </time>
        </p>

        {item.decision && (
          <div className="relative z-1 flex flex-wrap gap-2 pt-1.5">
            <Button
              size="sm"
              onClick={() => {
                markRead(item.id)
                onDecision?.(item, true)
              }}
            >
              {item.decision.accept}
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                markRead(item.id)
                onDecision?.(item, false)
              }}
            >
              {item.decision.reject}
            </Button>
          </div>
        )}
      </div>

      {/* Revealed on hover, but also on keyboard focus — a control that only
          exists under a pointer is a control half the users never reach. */}
      <div className="absolute top-1.5 right-1.5 z-1 flex gap-0.5 rounded-md bg-[var(--nx-surface)] p-0.5 opacity-0 shadow-[0_0_0_1px_var(--nx-hairline),var(--nx-elev-lift)] transition-opacity focus-within:opacity-100 group-hover/notif:opacity-100">
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={item.read ? t('row.markUnread') : t('row.markRead')}
          onClick={() => (item.read ? markUnread(item.id) : markRead(item.id))}
        >
          {item.read ? <Undo2 strokeWidth={1.7} /> : <Check strokeWidth={1.7} />}
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={t('row.dismiss')}
          onClick={() => dismiss(item.id)}
        >
          <X strokeWidth={1.7} />
        </Button>
      </div>
    </li>
  )
}
