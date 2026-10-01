import type { TFunction } from 'i18next'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { relativeTime } from '@/lib/datetime'
import { ACTIVITY_CATCH_UP_MS, useOpportunityActivity } from '../api'
import { formatMoney } from '../lib/format'
import type { OpportunityActivity } from '../schema'

/** One sentence per fact, using whatever labels the server could resolve; a missing label falls back to the bare kind. */
function describeActivity(t: TFunction<'opportunities'>, entry: OpportunityActivity): string {
  const amount = formatMoney(entry.amount, entry.currency)
  switch (entry.kind) {
    case 'created':
      return amount ? t('activity.kinds.createdWithAmount', { amount }) : t('activity.kinds.created')
    case 'opened':
      return entry.stageName ? t('activity.kinds.openedIn', { stage: entry.stageName }) : t('activity.kinds.opened')
    case 'stage_changed':
      if (entry.stageName && entry.fromStageName) return t('activity.kinds.stageChangedFromTo', { from: entry.fromStageName, to: entry.stageName })
      return entry.stageName ? t('activity.kinds.stageChangedTo', { to: entry.stageName }) : t('activity.kinds.stage_changed')
    case 'moved_pipeline':
      return entry.stageName ? t('activity.kinds.movedPipelineTo', { stage: entry.stageName }) : t('activity.kinds.moved_pipeline')
    case 'reassigned':
      return entry.principalName ? t('activity.kinds.reassignedTo', { name: entry.principalName }) : t('activity.kinds.reassigned')
    case 'won':
      return amount ? t('activity.kinds.wonWithAmount', { amount }) : t('activity.kinds.won')
    case 'lost':
      return entry.lostReason ? t('activity.kinds.lostWithReason', { reason: entry.lostReason }) : t('activity.kinds.lost')
    case 'archived':
      return t('activity.kinds.archived')
    case 'restored':
      return t('activity.kinds.restored')
    case 'custom_fields_changed':
      return entry.changedFields?.length
        ? t('activity.kinds.customFieldsChangedNamed', { fields: entry.changedFields.join(', ') })
        : t('activity.kinds.custom_fields_changed')
    default:
      return t('activity.kinds.unknown')
  }
}

/**
 * The opportunity's timeline — a projection of its published facts that the Worker fills a few seconds after each
 * change (adr-event-consumption.md, E-2 (a)). Read access is the opportunity's own, so this card never shows a fact
 * the page itself could not.
 */
export function ActivityCard({ opportunityId, rowVersion }: { opportunityId: number; rowVersion: number }) {
  const { t, i18n } = useTranslation('opportunities')
  const activity = useOpportunityActivity(opportunityId)
  const { refetch } = activity

  // A change to the record means a new fact is on its way: look again once the Worker has had time to project it,
  // even when interval polling is paused (a hidden tab).
  const seenVersion = useRef(rowVersion)
  useEffect(() => {
    if (seenVersion.current === rowVersion) return
    seenVersion.current = rowVersion
    const timers = ACTIVITY_CATCH_UP_MS.map((delay) => setTimeout(() => void refetch(), delay))
    return () => timers.forEach(clearTimeout)
  }, [rowVersion, refetch])
  const fullDate = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('activity.title')}</CardTitle>
        <CardDescription>{t('activity.description')}</CardDescription>
      </CardHeader>
      <CardContent>
        {activity.isError ? (
          <div className="grid justify-items-start gap-2 text-[13px]">
            <p className="text-muted-foreground">{t('activity.error')}</p>
            <Button type="button" variant="outline" size="sm" onClick={() => void activity.refetch()}>{t('activity.retry')}</Button>
          </div>
        ) : !activity.data ? (
          <div className="grid gap-2" aria-busy="true" aria-label={t('activity.loading')}>
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
          </div>
        ) : activity.data.length === 0 ? (
          <p className="text-muted-foreground text-[13px]">{t('activity.empty')}</p>
        ) : (
          <ol className="grid gap-3" aria-label={t('activity.title')}>
            {activity.data.map((entry) => (
              <li key={entry.eventId} className="border-border grid gap-0.5 border-l-2 pl-3 text-[13px]">
                <span className="min-w-0 break-words">{describeActivity(t, entry)}</span>
                <time className="text-muted-foreground text-xs" dateTime={entry.occurredAt} title={fullDate.format(new Date(entry.occurredAt))}>
                  {relativeTime(entry.occurredAt)}
                </time>
              </li>
            ))}
          </ol>
        )}
      </CardContent>
    </Card>
  )
}
