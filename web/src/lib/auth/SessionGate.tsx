import { useEffect, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Skeleton } from '@/components/ui/skeleton'
import { useSessionStore } from './session'
import { bootstrapSession } from './sessionClient'

/**
 * Wraps the router. Restores the session once at start-up and renders nothing
 * of the app until the answer is known — no flash of protected content, no
 * flash of the login form for someone who is in fact signed in.
 */
export function SessionGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation('auth')
  const status = useSessionStore((s) => s.status)

  useEffect(() => {
    void bootstrapSession()
  }, [])

  if (status === 'unknown') {
    return (
      <div className="bg-background flex min-h-screen items-center justify-center p-6" aria-busy="true" aria-live="polite">
        <span className="sr-only">{t('session.restoring')}</span>
        <Skeleton className="h-40 w-full max-w-sm rounded-xl" />
      </div>
    )
  }
  return <>{children}</>
}
