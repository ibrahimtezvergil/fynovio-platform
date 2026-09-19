import { RotateCw } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { LoadingRegion } from '@/features/demo-states/components/skeletons'

interface LoadingPreviewProps {
  /** Announced while the placeholder is up, and printed on the state chip. */
  label: string
  skeleton: ReactNode
  content: ReactNode
  /** How long the fake request takes, in ms. */
  duration?: number
}

/**
 * Runs the swap the reader is here to see: placeholder, then real content,
 * replayable. The delay is deliberately long enough (1.4s) to watch — a real
 * request that resolves faster than ~200ms should render nothing at all
 * rather than flash a skeleton.
 */
export function LoadingPreview({ label, skeleton, content, duration = 1400 }: LoadingPreviewProps) {
  const { t } = useTranslation('demo-states')
  const [isLoading, setIsLoading] = useState(true)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => {
    if (!isLoading) return
    timer.current = setTimeout(() => setIsLoading(false), duration)
    return () => clearTimeout(timer.current)
  }, [isLoading, duration])

  return (
    <div className="flex flex-col">
      <div className="flex items-center gap-2 border-b border-[var(--nx-hairline-soft)] bg-[var(--nx-fill)] py-1.5 pr-1.5 pl-3">
        <span
          aria-hidden
          data-tone={isLoading ? 'amber' : 'green'}
          className="nx-pill text-[11px]"
        >
          {isLoading ? t('loadingPreview.loadingPill') : t('loadingPreview.loadedPill')}
        </span>
        <span className="text-muted-foreground flex-1 truncate text-[11.5px]">{label}</span>
        <Button
          size="xs"
          variant="ghost"
          disabled={isLoading}
          onClick={() => setIsLoading(true)}
        >
          <RotateCw aria-hidden />
          {t('loadingPreview.retry')}
        </Button>
      </div>

      {isLoading ? (
        <LoadingRegion label={t('loadingPreview.regionLabel', { label })}>{skeleton}</LoadingRegion>
      ) : (
        content
      )}
    </div>
  )
}
