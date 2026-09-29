import { CircleAlert, Info, Settings2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
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

/**
 * Says, before the button is pressed, what the workspace settings will do to the new opportunity. It is background, not a step,
 * so it lives behind a button instead of taking a column of the form. Same row style as the detail page.
 */
export function OpportunityStartInfo({ summary, loading }: { summary: CreationSummary; loading: boolean }) {
  const { t } = useTranslation('opportunities')
  const userName = useSessionStore((state) => state.user?.name)
  const owner = summary.owner.kind === 'fixed' ? t('form.summary.ownerFixed') : userName ? t('form.summary.ownerSelf', { name: userName }) : t('form.summary.ownerSelfAnonymous')
  return (
    <Dialog>
      <DialogTrigger render={<Button type="button" variant="ghost" size="sm" />}>
        <Info aria-hidden strokeWidth={1.8} />
        {t('form.summary.button')}
      </DialogTrigger>
      <DialogContent aria-busy={loading || undefined}>
        <DialogHeader>
          <DialogTitle>{t('form.summary.title')}</DialogTitle>
          <DialogDescription>{t('form.summary.description')}</DialogDescription>
        </DialogHeader>
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
        <Link to={paths.crmSettings} className="text-[var(--nx-tint)] inline-flex items-center gap-1.5 text-[12.5px] font-[550] hover:underline">
          <Settings2 aria-hidden className="size-3.5" strokeWidth={1.8} />
          {t('form.summary.settings')}
        </Link>
      </DialogContent>
    </Dialog>
  )
}

/** Stays on the page (never in the popup): a create that cannot succeed has to be seen before the person fills anything in. */
export function OpportunityStartBlocker({ summary }: { summary: CreationSummary }) {
  const { t } = useTranslation('opportunities')
  if (summary.blocker?.kind !== 'unsupported') return null
  return (
    <div role="alert" className="border-destructive/40 bg-destructive/5 flex gap-2.5 rounded-[var(--nx-r-card)] border px-4 py-3 text-[12.5px]">
      <CircleAlert aria-hidden className="text-destructive mt-0.5 size-4 shrink-0" strokeWidth={1.8} />
      <div className="grid gap-1">
        <p className="font-medium">{t('form.summary.blocked.title')}</p>
        <p className="text-muted-foreground">{t(`form.summary.blocked.${summary.blocker.mode}`)}</p>
      </div>
    </div>
  )
}
