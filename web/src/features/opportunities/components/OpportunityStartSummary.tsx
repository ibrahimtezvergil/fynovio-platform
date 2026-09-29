import { CircleAlert, Settings2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import type { CreationSummary } from '../lib/creationSummary'

/** Says, before the button is pressed, what the workspace settings will do to the new opportunity. */
export function OpportunityStartSummary({ summary, loading }: { summary: CreationSummary; loading: boolean }) {
  const { t } = useTranslation('opportunities')
  const userName = useSessionStore((state) => state.user?.name)
  const owner = summary.owner.kind === 'fixed' ? t('form.summary.ownerFixed') : userName ? t('form.summary.ownerSelf', { name: userName }) : t('form.summary.ownerSelfAnonymous')
  const rows: readonly { label: string; value: string; hint?: string; muted?: boolean }[] = [
    { label: t('form.summary.owner'), value: summary.owner.kind === 'unsupported' ? '—' : owner },
    { label: t('form.summary.type'), value: summary.typeName ?? t('form.summary.typeNone'), muted: !summary.typeName },
    summary.pipelineName
      ? { label: t('form.summary.pipeline'), value: summary.entryStageName ? t('form.summary.pipelineEntry', { pipeline: summary.pipelineName, stage: summary.entryStageName }) : summary.pipelineName }
      : { label: t('form.summary.pipeline'), value: t('form.summary.pipelineNone'), hint: t('form.summary.pipelineNoneHint'), muted: true },
  ]
  return (
    <aside aria-label={t('form.summary.title')} className="grid gap-3 lg:sticky lg:top-[88px]">
      <Card className="gap-4 px-5 pt-[18px] pb-5" aria-busy={loading || undefined}>
        <div className="flex flex-col gap-0.5">
          <h2 className="font-heading text-[15px] leading-tight font-[620]">{t('form.summary.title')}</h2>
          <p className="text-muted-foreground text-[12.5px]">{t('form.summary.description')}</p>
        </div>
        <dl className="grid gap-3 text-[13px]">
          {rows.map((row) => (
            <div key={row.label} className="grid gap-0.5">
              <dt className="text-muted-foreground text-[12px]">{row.label}</dt>
              <dd className={row.muted ? 'text-muted-foreground' : 'font-medium'}>{row.value}</dd>
              {row.hint && <dd className="text-muted-foreground text-[12px]">{row.hint}</dd>}
            </div>
          ))}
          <div className="grid gap-0.5">
            <dt className="text-muted-foreground text-[12px]">{t('form.summary.status')}</dt>
            <dd className="flex items-center gap-2"><Badge variant="warning">{t('form.summary.statusValue')}</Badge></dd>
            <dd className="text-muted-foreground text-[12px]">{t('form.summary.statusHint')}</dd>
          </div>
        </dl>
        <Link to={paths.crmSettings} className="text-[var(--nx-tint)] inline-flex items-center gap-1.5 text-[12.5px] font-[550] hover:underline">
          <Settings2 aria-hidden className="size-3.5" strokeWidth={1.8} />
          {t('form.summary.settings')}
        </Link>
      </Card>
      {summary.blocker?.kind === 'unsupported' && (
        <div role="alert" className="border-destructive/40 bg-destructive/5 flex gap-2.5 rounded-[var(--nx-r-card)] border px-4 py-3 text-[12.5px]">
          <CircleAlert aria-hidden className="text-destructive mt-0.5 size-4 shrink-0" strokeWidth={1.8} />
          <div className="grid gap-1">
            <p className="font-medium">{t('form.summary.blocked.title')}</p>
            <p className="text-muted-foreground">{t(`form.summary.blocked.${summary.blocker.mode}`)}</p>
          </div>
        </div>
      )}
    </aside>
  )
}
