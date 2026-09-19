import { useTranslation } from 'react-i18next'
import { ChartCard } from '@/features/demo-charts/components/ChartCard'
import { QUOTA_ATTAINMENT } from '@/features/demo-charts/data/crm'
import { moneyShort, percent } from '@/features/demo-charts/data/format'

const remaining = QUOTA_ATTAINMENT.target - QUOTA_ATTAINMENT.closed

/**
 * A single ratio against a limit is not a chart — it is a figure and a track,
 * which is what `.nx-meter` already is everywhere else in this app. A radial
 * gauge would draw the same one number as a two-slice pie and read worse at
 * card size, so this card is the deliberate absence of a chart.
 */
export function QuotaMeterCard() {
  const { t } = useTranslation('demo-charts')

  return (
    <ChartCard
      title={t('quotaMeter.title')}
      description={t('quotaMeter.description')}
      note={t('quotaMeter.note')}
    >
      <div className="flex flex-col gap-3">
        <p className="font-heading tnum text-[38px] leading-none font-semibold tracking-[-0.04em]">
          {percent(QUOTA_ATTAINMENT.percent)}
          <span className="text-muted-foreground ml-2 align-middle text-[13px] font-medium tracking-normal">
            {t('quotaMeter.ofQuarterTarget')}
          </span>
        </p>
        <div
          className="nx-meter"
          role="img"
          aria-label={t('quotaMeter.ariaLabel', { percent: QUOTA_ATTAINMENT.percent })}
        >
          <div className="nx-meter__bar" style={{ width: `${QUOTA_ATTAINMENT.percent}%` }} />
        </div>
      </div>

      <dl className="grid grid-cols-3 gap-4 text-[12.5px]">
        <div className="flex flex-col gap-1">
          <dt className="text-muted-foreground">{t('quotaMeter.closed')}</dt>
          <dd className="tnum text-[15px] font-[590]">{moneyShort(QUOTA_ATTAINMENT.closed)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-muted-foreground">{t('quotaMeter.target')}</dt>
          <dd className="tnum text-[15px] font-[590]">{moneyShort(QUOTA_ATTAINMENT.target)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-muted-foreground">{t('quotaMeter.remaining')}</dt>
          <dd className="tnum text-[15px] font-[590]">{moneyShort(remaining)}</dd>
        </div>
      </dl>
    </ChartCard>
  )
}
