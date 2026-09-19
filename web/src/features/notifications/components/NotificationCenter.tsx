import { Bell, CheckCheck, Inbox, Settings2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Button, buttonVariants } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { NotificationList } from '@/features/notifications/components/NotificationList'
import {
  useNotificationStore,
  useUnreadCount,
} from '@/features/notifications/store/useNotificationStore'
import {
  matchesFilter,
  useFilterLabels,
  type AppNotification,
  type NotificationFilter,
} from '@/features/notifications/types'
import { paths } from '@/routes/paths'

const FILTER_VALUES = ['all', 'unread', 'task', 'system'] as const

/**
 * The bell, its popover and the full drawer behind it.
 *
 * Two surfaces, one list. The popover is the glance — the last handful of
 * events, close to the bell that announced them. The drawer is the archive:
 * same rows, grouped by day, with room to work through a backlog. Anything
 * that had to be decided twice (which rows, which order, what read means)
 * lives in the store, so the two can never disagree.
 */
export function NotificationCenter() {
  const { t } = useTranslation('notifications')
  const filterLabels = useFilterLabels()
  const [open, setOpen] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [filter, setFilter] = useState<NotificationFilter>('all')

  const items = useNotificationStore((state) => state.items)
  const markAllRead = useNotificationStore((state) => state.markAllRead)
  const clearAll = useNotificationStore((state) => state.clearAll)
  const unread = useUnreadCount()

  const visible = useMemo(() => items.filter((item) => matchesFilter(item, filter)), [items, filter])

  const filters: readonly Segment<NotificationFilter>[] = useMemo(
    () => FILTER_VALUES.map((value) => ({ value, label: filterLabels[value] })),
    [filterLabels],
  )

  const handleOpen = (item: AppNotification) => {
    setOpen(false)
    setDrawerOpen(false)
    toast.info(item.title, { description: t('toast.openDescription') })
  }

  const handleDecision = (item: AppNotification, accepted: boolean) => {
    if (accepted) toast.success(t('toast.accepted'), { description: item.title })
    else toast.warning(t('toast.rejected'), { description: item.title })
  }

  const filterControl = (
    <SegmentedControl
      aria-label={t('center.filterLabel')}
      segments={filters}
      value={filter}
      onChange={setFilter}
      fullWidth
    />
  )

  return (
    <>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger
          render={
            <Button
              variant="secondary"
              size="icon"
              className="relative"
              aria-label={
                unread > 0
                  ? t('center.bellUnread', { count: unread })
                  : t('center.bellAllRead')
              }
            />
          }
        >
          <Bell aria-hidden className="size-[17px]" strokeWidth={1.7} />
          {unread > 0 && (
            /* Redundant by design: the count is already in the button's
               accessible name, so the badge only has to be *noticed*. Bold at
               10.5px is the artboard's rule for a label on the accent fill. */
            <span
              aria-hidden
              className="absolute -top-1 -right-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full border border-white/25 bg-[image:var(--nx-accent-grad)] px-1 text-[10.5px] leading-none font-[750] text-[var(--nx-on-accent)] shadow-[var(--nx-accent-glow)]"
            >
              {unread > 9 ? '9+' : unread}
            </span>
          )}
        </PopoverTrigger>

        <PopoverContent align="end" sideOffset={10} className="w-[392px] gap-0 p-0">
          <div className="flex items-center gap-2 px-4 pt-3.5 pb-2.5">
            <h2 className="font-heading flex-1 text-[15px] font-[620] tracking-[-0.022em]">
              {t('center.title')}
            </h2>
            <Button
              variant="ghost"
              size="sm"
              disabled={unread === 0}
              onClick={() => markAllRead()}
            >
              <CheckCheck strokeWidth={1.7} />
              {t('center.markAllRead')}
            </Button>
          </div>

          <div className="px-4 pb-3">{filterControl}</div>

          <div className="max-h-[382px] overflow-y-auto border-t border-[var(--nx-hairline)]">
            <NotificationList
              items={visible}
              onOpen={handleOpen}
              onDecision={handleDecision}
              emptyTitle={filter === 'unread' ? t('center.emptyAllReadTitle') : t('center.emptyTitle')}
              emptyDescription={
                filter === 'unread'
                  ? t('center.emptyAllReadDescription')
                  : t('center.emptyFilteredDescription')
              }
            />
          </div>

          <div className="flex items-center gap-2 border-t border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-3 py-2.5">
            <Button
              variant="ghost"
              size="sm"
              className="flex-1"
              onClick={() => {
                setOpen(false)
                setDrawerOpen(true)
              }}
            >
              <Inbox strokeWidth={1.7} />
              {t('center.viewAll')}
            </Button>
            {/* A link, styled as a button — not a Button rendering a link: Base
                UI's Button asserts a native <button> underneath, and an anchor
                there loses the semantics both the router and the reader need. */}
            <Link
              to={paths.settings}
              aria-label={t('center.settingsLink')}
              onClick={() => setOpen(false)}
              className={buttonVariants({ variant: 'ghost', size: 'icon-sm' })}
            >
              <Settings2 aria-hidden strokeWidth={1.7} />
            </Link>
          </div>
        </PopoverContent>
      </Popover>

      <Sheet open={drawerOpen} onOpenChange={setDrawerOpen}>
        <SheetContent className="gap-0 sm:max-w-lg">
          <SheetHeader>
            <SheetTitle>{t('center.drawerTitle')}</SheetTitle>
            <SheetDescription>
              {unread > 0 ? t('center.drawerUnreadPrefix', { count: unread }) : ''}
              {t('center.drawerCount', { count: items.length })}
            </SheetDescription>
          </SheetHeader>

          <div className="px-5 pt-4 pb-3">{filterControl}</div>

          <div className="flex-1 overflow-y-auto border-t border-[var(--nx-hairline)]">
            <NotificationList
              items={visible}
              grouped
              onOpen={handleOpen}
              onDecision={handleDecision}
              emptyTitle={filter === 'unread' ? t('center.emptyAllReadTitle') : t('center.emptyTitle')}
              emptyDescription={t('center.emptyFilteredDescription')}
            />
          </div>

          <SheetFooter>
            <Button variant="ghost" disabled={items.length === 0} onClick={() => clearAll()}>
              {t('center.clearAll')}
            </Button>
            <SheetClose render={<Button variant="outline" />}>{t('center.close')}</SheetClose>
          </SheetFooter>
        </SheetContent>
      </Sheet>
    </>
  )
}
