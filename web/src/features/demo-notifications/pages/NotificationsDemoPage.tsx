import {
  BellRing,
  CircleAlert,
  CircleCheck,
  Info,
  Loader2,
  TriangleAlert,
  Undo2,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { PageHeader } from '@/components/common/PageHeader'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { DemoSection } from '@/components/common/DemoSection'
import { ToastCard } from '@/features/demo-notifications/components/ToastCard'

/**
 * Toast (sonner, bottom-right) for the outcome of an action, Alert (inline,
 * `src/components/ui/alert.tsx`) for state the page itself is in. Both ride
 * the same four tones as `Badge`/`.nx-pill` — success/warning/info/error —
 * so a toast and an inline banner never disagree about what green means.
 */
export default function NotificationsDemoPage() {
  const { t } = useTranslation('demo-notifications')
  const rules = t('page.rules', { returnObjects: true }) as [string, string][]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
      />

      <DemoSection
        id="toast"
        title={t('sections.toast.title')}
        description={t('sections.toast.description')}
        icon={BellRing}
      >
        <ToastCard
          tone="success"
          icon={<CircleCheck className="size-4" strokeWidth={1.75} />}
          title={t('toast.success.title')}
          description={t('toast.success.description')}
          code={`toast.success('${t('toast.success.message')}')`}
        >
          <Button
            size="sm"
            variant="outline"
            onClick={() => toast.success(t('toast.success.message'))}
          >
            {t('toast.success.trigger')}
          </Button>
        </ToastCard>

        <ToastCard
          tone="error"
          icon={<CircleAlert className="size-4" strokeWidth={1.75} />}
          title={t('toast.error.title')}
          description={t('toast.error.description')}
          code={`toast.error('${t('toast.error.message')}')`}
        >
          <Button
            size="sm"
            variant="outline"
            onClick={() => toast.error(t('toast.error.message'))}
          >
            {t('toast.error.trigger')}
          </Button>
        </ToastCard>

        <ToastCard
          tone="warning"
          icon={<TriangleAlert className="size-4" strokeWidth={1.75} />}
          title={t('toast.warning.title')}
          description={t('toast.warning.description')}
          code={`toast.warning('${t('toast.warning.message')}')`}
        >
          <Button
            size="sm"
            variant="outline"
            onClick={() => toast.warning(t('toast.warning.message'))}
          >
            {t('toast.warning.trigger')}
          </Button>
        </ToastCard>

        <ToastCard
          tone="info"
          icon={<Info className="size-4" strokeWidth={1.75} />}
          title={t('toast.info.title')}
          description={t('toast.info.description')}
          code={`toast.info('${t('toast.info.message')}')`}
        >
          <Button
            size="sm"
            variant="outline"
            onClick={() => toast.info(t('toast.info.message'))}
          >
            {t('toast.info.trigger')}
          </Button>
        </ToastCard>

        <ToastCard
          tone="error"
          icon={<Undo2 className="size-4" strokeWidth={1.75} />}
          title={t('toast.action.title')}
          description={t('toast.action.description')}
          code={`toast('${t('toast.action.message')}', { action: { label: '${t('toast.action.undoLabel')}', onClick } })`}
          wide
        >
          <Button
            size="sm"
            variant="outline"
            onClick={() =>
              toast(t('toast.action.message'), {
                description: t('toast.action.deletedDescription'),
                action: {
                  label: t('toast.action.undoLabel'),
                  onClick: () => toast.success(t('toast.action.restoredMessage')),
                },
              })
            }
          >
            {t('toast.action.trigger')}
          </Button>
        </ToastCard>

        <ToastCard
          tone="info"
          icon={<Loader2 className="size-4" strokeWidth={1.75} />}
          title={t('toast.promise.title')}
          description={t('toast.promise.description')}
          code={"toast.promise(request, { loading, success, error })"}
          wide
        >
          <Button
            size="sm"
            variant="outline"
            onClick={() =>
              toast.promise(
                new Promise<void>((resolve) => setTimeout(resolve, 1600)),
                {
                  loading: t('toast.promise.loading'),
                  success: t('toast.promise.success'),
                  error: t('toast.promise.error'),
                },
              )
            }
          >
            {t('toast.promise.trigger')}
          </Button>
        </ToastCard>
      </DemoSection>

      <DemoSection
        id="alert"
        title={t('sections.alert.title')}
        description={t('sections.alert.description')}
        icon={CircleAlert}
      >
        <Alert variant="success">
          <CircleCheck />
          <AlertTitle>{t('alerts.twoFactor.title')}</AlertTitle>
          <AlertDescription>{t('alerts.twoFactor.description')}</AlertDescription>
        </Alert>

        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>{t('alerts.payment.title')}</AlertTitle>
          <AlertDescription>{t('alerts.payment.description')}</AlertDescription>
        </Alert>

        <Alert variant="warning">
          <TriangleAlert />
          <AlertTitle>{t('alerts.storage.title')}</AlertTitle>
          <AlertDescription>{t('alerts.storage.description')}</AlertDescription>
        </Alert>

        <Alert variant="info">
          <Info />
          <AlertTitle>{t('alerts.maintenance.title')}</AlertTitle>
          <AlertDescription>{t('alerts.maintenance.description')}</AlertDescription>
        </Alert>
      </DemoSection>

      <Card className="gap-3.5 px-5 pt-[19px] pb-5">
        <h2 className="font-heading text-[15.5px] leading-snug font-[620] tracking-[-0.022em]">
          {t('page.rulesHeading')}
        </h2>
        <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
          {rules.map(([term, detail]) => (
            <div key={term} className="flex flex-col gap-0.5">
              <dt className="text-[13px] font-[590]">{term}</dt>
              <dd className="text-muted-foreground text-[12.5px] leading-[1.5]">{detail}</dd>
            </div>
          ))}
        </dl>
      </Card>
    </div>
  )
}
