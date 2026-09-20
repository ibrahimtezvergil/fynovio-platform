import { FileQuestion, Lock, RefreshCw, ServerCrash } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'
import { toProblem } from '../lib/problem'
import type { ApiError } from '@/types'

interface QueryProblemStateProps {
  error: unknown
  onRetry: () => void
  retrying?: boolean
  /** Extra action next to Retry (e.g. back to the list). */
  action?: React.ReactNode
}

/**
 * A failed READ, as a state of the page rather than a crash. 403 says "forbidden"; 404 says "not found or not
 * accessible" and is deliberately identical for a missing, a denied and a cross-tenant record (the API
 * collapses those on purpose — the UI must not un-collapse them).
 */
export function QueryProblemState({ error, onRetry, retrying, action }: QueryProblemStateProps) {
  const { t } = useTranslation('opportunities')
  const problem = toProblem(error as ApiError)

  if (problem.kind === 'forbidden')
    return <EmptyState icon={Lock} tone="warning" title={t('state.forbidden.title')} description={t('state.forbidden.description')} action={action} />
  if (problem.kind === 'notFound')
    return <EmptyState icon={FileQuestion} title={t('state.notFound.title')} description={t('state.notFound.description')} action={action} />

  return (
    <EmptyState
      icon={ServerCrash}
      tone="danger"
      title={t('state.error.title')}
      description={t('state.error.description')}
      action={
        <>
          <Button onClick={onRetry} disabled={retrying}>
            <RefreshCw aria-hidden />
            {t('state.error.retry')}
          </Button>
          {action}
        </>
      }
    />
  )
}
