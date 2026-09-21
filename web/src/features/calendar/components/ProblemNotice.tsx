import { AlertTriangle, RefreshCw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { describeProblem, needsReload, type Problem } from '../lib/problem'

interface ProblemNoticeProps {
  problem: Problem
  /** Re-reads the entry (stale-write conflict, entry gone); offered only for those kinds. Never resubmits anything itself. */
  onReload?: () => void
  /** Re-runs a failed read; offered for the kinds a retry can clear. */
  onRetry?: () => void
  className?: string
}

/** A failed request, in the user's terms and language. Raw server text never reaches here (see `toProblem`). */
export function ProblemNotice({ problem, onReload, onRetry, className }: ProblemNoticeProps) {
  const { t } = useTranslation('calendar')
  const { title, description } = describeProblem(t, problem)
  const retryable = problem.kind === 'unavailable' || problem.kind === 'rateLimited'
  return (
    <Alert variant="destructive" role="alert" data-problem={problem.kind} className={className}>
      <AlertTriangle aria-hidden />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p>{description}</p>
        {onReload && needsReload(problem) && (
          <Button type="button" variant="outline" size="sm" className="mt-2" onClick={onReload}>
            <RefreshCw aria-hidden />
            {t('problem.reload')}
          </Button>
        )}
        {onRetry && retryable && (
          <Button type="button" variant="outline" size="sm" className="mt-2" onClick={onRetry}>
            <RefreshCw aria-hidden />
            {t('problem.retry')}
          </Button>
        )}
      </AlertDescription>
    </Alert>
  )
}
