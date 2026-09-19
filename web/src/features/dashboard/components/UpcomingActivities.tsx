import type { TFunction } from 'i18next'
import { ChevronRight, Clock, Mail, MessageSquare, ShieldCheck, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import type { Activity, ActivityKind } from '@/types'

import { Skeleton } from '@/components/ui/skeleton'
type Tone = 'amber' | 'blue' | 'purple' | 'teal'

const KIND_ICON_TONE: Record<ActivityKind, { icon: LucideIcon; tone: Tone }> = {
  task: { icon: Clock, tone: 'amber' },
  message: { icon: MessageSquare, tone: 'blue' },
  email: { icon: Mail, tone: 'purple' },
  review: { icon: ShieldCheck, tone: 'teal' },
}

function kindLabel(kind: ActivityKind, t: TFunction<'dashboard'>): string {
  return t(`upcomingActivities.kind${kind.charAt(0).toUpperCase()}${kind.slice(1)}` as const)
}

/**
 * Next touchpoints, newest first. The tile's tone is decoration — the kind is
 * also on the row's accessible name, so it survives greyscale (WCAG 1.4.1).
 */
export function UpcomingActivities({
  activities,
  isLoading,
}: {
  activities: Activity[]
  isLoading?: boolean
}) {
  const { t } = useTranslation('dashboard')
  return (
    <Card className="gap-0 p-0">
      <div className="flex items-center gap-2.5 px-5 pt-[19px] pb-3.5">
        <h2 className="font-heading flex-1 text-[15.5px] leading-snug font-[620] tracking-[-0.022em]">
          {t('upcomingActivities.heading')}
        </h2>
        <Button variant="ghost" size="sm">
          {t('upcomingActivities.viewAll')}
        </Button>
      </div>

      <div className="flex flex-col border-t border-[var(--nx-hairline)]">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="nx-row px-5">
                <Skeleton className="size-[30px] rounded-sm" />
                <Skeleton className="h-3.5 flex-1" />
              </div>
            ))
          : activities.map((activity) => {
              const { icon: Icon, tone } = KIND_ICON_TONE[activity.kind]
              const label = kindLabel(activity.kind, t)
              return (
                <button
                  key={activity.id}
                  type="button"
                  className="nx-row nx-row--tap w-full px-5 text-left"
                >
                  <span aria-hidden data-tone={tone} className="nx-icon-tile">
                    <Icon className="size-4" strokeWidth={1.7} />
                  </span>
                  <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                    <span className="truncate text-[13px] font-[550] tracking-[-0.012em]">
                      <span className="sr-only">{label}: </span>
                      {activity.title}
                    </span>
                    <span className="tnum text-muted-foreground text-[11.5px]">{activity.when}</span>
                  </span>
                  <ChevronRight
                    aria-hidden
                    className="text-muted-foreground size-[15px] shrink-0"
                    strokeWidth={1.7}
                  />
                </button>
              )
            })}
      </div>
    </Card>
  )
}
