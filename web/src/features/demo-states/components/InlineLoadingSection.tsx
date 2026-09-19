import { Loader2, RefreshCw, Save, Timer } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StateCard } from '@/features/demo-states/components/StateCard'
import { ListLoaded, TableLoaded } from '@/features/demo-states/components/loaded'
import { cn } from '@/lib/utils'

/** Runs a fake request once and reports whether it is still in flight. */
function usePending(duration: number) {
  const [isPending, setIsPending] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  const start = () => {
    if (isPending) return
    setIsPending(true)
    timer.current = setTimeout(() => setIsPending(false), duration)
  }

  return { isPending, start }
}

/** A submit that keeps its width so the row does not reflow mid-request. */
function PendingButton() {
  const { t } = useTranslation('demo-states')
  const { isPending, start } = usePending(1600)

  return (
    <div className="flex items-center gap-3 p-4">
      <Button onClick={start} disabled={isPending} className="min-w-[132px]">
        {isPending ? <Loader2 className="animate-spin" aria-hidden /> : <Save aria-hidden />}
        {isPending ? t('inlineLoading.saving') : t('inlineLoading.save')}
      </Button>
      <p className="text-muted-foreground text-[12px]">
        {isPending ? t('inlineLoading.pendingHint') : t('inlineLoading.idleHint')}
      </p>
    </div>
  )
}

/**
 * A refetch over data that is already on screen. The old rows stay legible at
 * reduced opacity; replacing them with a skeleton would throw away something
 * the reader was in the middle of reading.
 */
function RefetchOverlay() {
  const { t } = useTranslation('demo-states')
  const { isPending, start } = usePending(1600)

  return (
    <div className="flex flex-col">
      <div className="flex items-center gap-2 border-b border-[var(--nx-hairline-soft)] bg-[var(--nx-fill)] py-1.5 pr-1.5 pl-3">
        <span className="text-muted-foreground flex-1 text-[11.5px]">
          {isPending ? t('inlineLoading.updating') : t('inlineLoading.current')}
        </span>
        <Button size="xs" variant="ghost" onClick={start} disabled={isPending}>
          <RefreshCw aria-hidden className={cn(isPending && 'animate-spin')} />
          {t('inlineLoading.refresh')}
        </Button>
      </div>
      <div
        aria-busy={isPending}
        className={cn('transition-opacity duration-[250ms]', isPending && 'opacity-50')}
      >
        <TableLoaded />
      </div>
    </div>
  )
}

/** Appending a page: the loaded rows never move, the placeholder grows below. */
function LoadMore() {
  const { t } = useTranslation('demo-states')
  const { isPending, start } = usePending(1500)

  return (
    <div className="flex flex-col">
      <ListLoaded />
      {isPending ? (
        <div
          role="status"
          aria-live="polite"
          className="flex flex-col gap-2.5 border-t border-[var(--nx-hairline-soft)] p-4"
        >
          <span className="sr-only">{t('inlineLoading.nextPageLoading')}</span>
          <Skeleton className="h-3 w-[58%]" />
          <Skeleton className="h-3 w-[42%]" />
        </div>
      ) : (
        <div className="border-t border-[var(--nx-hairline-soft)] p-3 text-center">
          <Button size="sm" variant="outline" onClick={start}>
            {t('inlineLoading.loadMore')}
          </Button>
        </div>
      )}
    </div>
  )
}

/**
 * Loading that is not a whole page. The rule the three share: never take away
 * something the reader already has. A pending button keeps its width, a
 * refetch keeps its rows, an appended page keeps everything above it still.
 */
export function InlineLoadingSection() {
  const { t } = useTranslation('demo-states')

  return (
    <DemoSection
      id="kismi"
      title={t('inlineLoading.title')}
      description={t('inlineLoading.description')}
      icon={Timer}
    >
      <StateCard
        title={t('inlineLoading.refetchCard.title')}
        description={t('inlineLoading.refetchCard.description')}
      >
        <RefetchOverlay />
      </StateCard>

      <StateCard
        title={t('inlineLoading.loadMoreCard.title')}
        description={t('inlineLoading.loadMoreCard.description')}
      >
        <LoadMore />
      </StateCard>

      <StateCard
        title={t('inlineLoading.pendingCard.title')}
        description={t('inlineLoading.pendingCard.description')}
        wide
      >
        <PendingButton />
      </StateCard>
    </DemoSection>
  )
}
