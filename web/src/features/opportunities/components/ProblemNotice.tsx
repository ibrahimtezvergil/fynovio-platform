import { AlertTriangle, RefreshCw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { describeProblem, needsReload, type Problem } from '../lib/problem'

interface ProblemNoticeProps {
  problem: Problem
  /** Re-reads the record; offered for a stale-write conflict. Never resubmits anything by itself. */
  onReload?: () => void
  className?: string
}

/** A failed command, in the user's terms. Stack traces and raw server text never reach here (see `toProblem`). */
export function ProblemNotice({ problem, onReload, className }: ProblemNoticeProps) {
  const { t } = useTranslation('opportunities')
  const { title, description } = describeProblem(t, problem)
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
      </AlertDescription>
    </Alert>
  )
}
