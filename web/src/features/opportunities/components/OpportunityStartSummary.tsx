import { CircleAlert, Settings2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import type { CreationSummary } from '../lib/creationSummary'

function Row({ label, children, hint }: { label: string; children: React.ReactNode; hint?: string }) {
  return (
    <div className="grid grid-cols-[110px_1fr] gap-3 py-2 text-[13px]">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="min-w-0 break-words">
        {children}
        {hint && <p className="text-muted-foreground mt-0.5 text-[12px]">{hint}</p>}
      </dd>
    </div>
  )
}

/** Says, before the button is pressed, what the workspace settings will do to the new opportunity. Same card and row style as the detail page. */
export function OpportunityStartSummary({ summary, loading }: { summary: CreationSummary; loading: boolean }) {
  const { t } = useTranslation('opportunities')
  const userName = useSessionStore((state) => state.user?.name)
  const owner = summary.owner.kind === 'fixed' ? t('form.summary.ownerFixed') : userName ? t('form.summary.ownerSelf', { name: userName }) : t('form.summary.ownerSelfAnonymous')
  return (
    <aside aria-label={t('form.summary.title')} className="grid content-start gap-4 lg:sticky lg:top-[88px]">
      <Card aria-busy={loading || undefined}>
        <CardHeader>
          <CardTitle>{t('form.summary.title')}</CardTitle>
          <CardDescription>{t('form.summary.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="divide-border/60 divide-y">
            <Row label={t('form.summary.owner')}>{summary.owner.kind === 'unsupported' ? '—' : owner}</Row>
            <Row label={t('form.summary.type')}>
              {summary.typeName ?? <span className="text-muted-foreground">{t('form.summary.typeNone')}</span>}
            </Row>
            <Row label={t('form.summary.pipeline')} hint={summary.pipelineName ? undefined : t('form.summary.pipelineNoneHint')}>
              {summary.pipelineName
                ? summary.entryStageName ? t('form.summary.pipelineEntry', { pipeline: summary.pipelineName, stage: summary.entryStageName }) : summary.pipelineName
                : <span className="text-muted-foreground">{t('form.summary.pipelineNone')}</span>}
            </Row>
            <Row label={t('form.summary.status')} hint={t('form.summary.statusHint')}>
              <Badge variant="warning">{t('form.summary.statusValue')}</Badge>
            </Row>
          </dl>
          <Link to={paths.crmSettings} className="text-[var(--nx-tint)] mt-3 inline-flex items-center gap-1.5 text-[12.5px] font-[550] hover:underline">
            <Settings2 aria-hidden className="size-3.5" strokeWidth={1.8} />
            {t('form.summary.settings')}
          </Link>
        </CardContent>
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
