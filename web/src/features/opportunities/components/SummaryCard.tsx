import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDate, formatMoney } from '../lib/format'
import type { AvailableActions, Opportunity } from '../schema'

function Row({ label, children, testId }: { label: string; children: React.ReactNode; testId?: string }) {
  if (children === null || children === undefined || children === false) return null
  return (
    <div className="grid grid-cols-[150px_1fr] gap-3 py-1.5 text-[13px]">
      <dt className="text-muted-foreground">{label}</dt>
      <dd data-testid={testId} className="min-w-0 break-words">
        {children}
      </dd>
    </div>
  )
}

/**
 * Renders what the DTO carries and nothing else: a field the backend omitted is simply absent (never inferred
 * or reconstructed elsewhere), and the row version is deliberately not shown.
 */
export function SummaryCard({ opportunity, actions }: { opportunity: Opportunity; actions: AvailableActions | undefined }) {
  const { t } = useTranslation('opportunities')
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('summary.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="divide-border/60 divide-y">
          <Row label={t('summary.party')} testId="summary-party">
            {opportunity.partyId != null && t('summary.partyValue', { id: opportunity.partyId })}
          </Row>
          <Row label={t('summary.estimatedAmount')} testId="summary-estimated">
            {formatMoney(opportunity.estimatedAmount, opportunity.currency)}
          </Row>
          <Row label={t('summary.totalAmount')} testId="summary-total">
            {formatMoney(opportunity.totalAmount, opportunity.currency)}
          </Row>
          <Row label={t('summary.owner')} testId="summary-owner">
            {opportunity.assignedPrincipalSubject && (
              <span className="grid gap-1">
                <code className="text-[12px]">{opportunity.assignedPrincipalSubject}</code>
                {actions?.canReassign && (
                  // Plan OD1: the command exists but no eligible-assignee source does, so no control collects a principal.
                  <span data-testid="reassign-dependency" className="text-muted-foreground text-[12px]">
                    {t('summary.reassignDependency')}
                  </span>
                )}
              </span>
            )}
          </Row>
          <Row label={t('summary.expiryDate')}>{formatDate(opportunity.expiryDate)}</Row>
          <Row label={t('summary.openedDate')}>{formatDate(opportunity.openedDate)}</Row>
          <Row label={t('summary.wonDate')}>{formatDate(opportunity.wonDate)}</Row>
          <Row label={t('summary.lostDate')}>{formatDate(opportunity.lostDate)}</Row>
          <Row label={t('summary.lostReason')} testId="summary-lost-reason">
            {opportunity.lostReason}
          </Row>
        </dl>
      </CardContent>
    </Card>
  )
}
