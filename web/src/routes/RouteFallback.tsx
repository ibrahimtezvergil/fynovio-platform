import { useTranslation } from 'react-i18next'
import { Skeleton } from '@/components/ui/skeleton'

/** Shown while a lazily-loaded route chunk is in flight. */
export function RouteFallback() {
  const { t } = useTranslation('routes')
  return (
    <div className="flex flex-col gap-4" aria-busy="true" aria-live="polite">
      <span className="sr-only">{t('loading')}</span>
      <Skeleton className="h-9 w-48 rounded-md" />
      <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-[132px] rounded-xl" />
        ))}
      </div>
      <Skeleton className="h-80 rounded-xl" />
    </div>
  )
}
