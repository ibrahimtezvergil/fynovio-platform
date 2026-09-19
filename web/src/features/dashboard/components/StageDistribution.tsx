import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { StageBadge } from '@/components/common/StageBadge'
import { Card } from '@/components/ui/card'
import { STAGES, type StageBucket } from '@/types'
import { paths } from '@/routes/paths'

import { Skeleton } from '@/components/ui/skeleton'
const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})

/**
 * Whole-pipeline roll-up beside the grid. Meters are relative to the largest
 * bucket, not to the total — the comparison the artboard draws is stage vs.
 * stage, so the leader always fills the track.
 */
export function StageDistribution({
  buckets,
  isLoading,
}: {
  buckets: StageBucket[]
  isLoading?: boolean
}) {
  const { t } = useTranslation('dashboard')
  const navigate = useNavigate()
  if (isLoading) {
    return (
      <Card className="gap-4 px-5 pt-[19px] pb-5">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-[33px] rounded-md" />
        ))}
      </Card>
    )
  }

  const ordered = STAGES.map((stage) => buckets.find((b) => b.stage === stage)).filter(
    (b): b is StageBucket => b != null,
  )
  const total = ordered.reduce((sum, b) => sum + b.count, 0)
  const max = Math.max(...ordered.map((b) => b.value), 1)

  return (
    <Card className="gap-4 px-5 pt-[19px] pb-5">
      <div className="flex items-center gap-2.5">
        <h2 className="font-heading flex-1 text-[15.5px] leading-snug font-[620] tracking-[-0.022em]">
          {t('stageDistribution.heading')}
        </h2>
        <span className="nx-eyebrow tnum">{t('stageDistribution.dealCount', { count: total })}</span>
      </div>

      {ordered.map((bucket) => (
        <button
          key={bucket.stage}
          type="button"
          onClick={() => navigate(`${paths.crmPipeline}?stage=${bucket.stage}`)}
          className="group flex flex-col gap-[7px] rounded-sm text-left outline-none focus-visible:ring-2 focus-visible:ring-ring"
          aria-label={t('stageDistribution.drillThrough', { stage: bucket.stage, count: bucket.count })}
        >
          <div className="flex items-center gap-3">
            <StageBadge stage={bucket.stage} className="flex-none" />
            <span className="flex-1" />
            <span className="tnum text-muted-foreground min-w-[22px] text-right text-[12.5px]">
              {bucket.count}
            </span>
            <span className="tnum min-w-[88px] text-right text-[13px] font-[590]">
              {money.format(bucket.value)}
            </span>
          </div>
          <div className="nx-meter group-hover:opacity-80">
            <div className="nx-meter__bar" style={{ width: `${(bucket.value / max) * 100}%` }} />
          </div>
        </button>
      ))}
    </Card>
  )
}
