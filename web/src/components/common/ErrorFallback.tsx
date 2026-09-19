import { AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/common/EmptyState'

interface ErrorFallbackProps {
  title?: string
  description?: string
  /** Raw error detail — dev-only, never shown to a real user in production. */
  detail?: string
  onRetry: () => void
  retryLabel?: string
}

/**
 * The one "what failed, in the user's terms, plus a retry" surface
 * (`docs/design-system/09-states-and-feedback.md`), reused by both the root
 * `ErrorBoundary` and the router's `errorElement` so a render crash always
 * looks the same regardless of where it was caught.
 */
export function ErrorFallback({
  title = 'Bir şeyler ters gitti',
  description = 'Bu ekran yüklenirken beklenmeyen bir hata oluştu. Yeniden denemek çoğu zaman sorunu çözer.',
  detail,
  onRetry,
  retryLabel = 'Yeniden dene',
}: ErrorFallbackProps) {
  return (
    <div className="bg-background relative isolate flex min-h-screen flex-col items-center justify-center p-6">
      <div aria-hidden className="nx-ambient" />
      <div className="relative z-[1]">
        <EmptyState
          icon={AlertTriangle}
          tone="danger"
          title={title}
          description={description}
          action={<Button onClick={onRetry}>{retryLabel}</Button>}
        />
        {import.meta.env.DEV && detail && (
          <pre className="text-muted-foreground border-border/60 mt-4 max-w-lg overflow-auto rounded-[var(--nx-r-ctl-lg)] border p-3 text-left text-[11px] leading-[1.5] whitespace-pre-wrap">
            {detail}
          </pre>
        )}
      </div>
    </div>
  )
}
