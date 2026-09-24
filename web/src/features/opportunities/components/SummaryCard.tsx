import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { usePartyNames } from '../api'
import { formatDate, formatMoney } from '../lib/format'
import type { Opportunity } from '../schema'

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
export function SummaryCard({ opportunity, canReassign = false, onReassign }: { opportunity: Opportunity; canReassign?: boolean; onReassign?: () => void }) {
  const { t } = useTranslation('opportunities')
  // Best effort: a caller without the party-search permission (or a failed lookup) just keeps seeing the id.
  const partyIds = opportunity.partyId != null ? [opportunity.partyId] : []
  const partyName = usePartyNames(partyIds).data?.get(opportunity.partyId ?? -1)
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('summary.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="divide-border/60 divide-y">
          <Row label={t('summary.party')} testId="summary-party">
            {opportunity.partyId != null &&
              (partyName ? t('summary.partyNamed', { name: partyName, id: opportunity.partyId }) : t('summary.partyValue', { id: opportunity.partyId }))}
          </Row>
          <Row label={t('summary.estimatedAmount')} testId="summary-estimated">
            {formatMoney(opportunity.estimatedAmount, opportunity.currency)}
          </Row>
          <Row label={t('summary.totalAmount')} testId="summary-total">
            {formatMoney(opportunity.totalAmount, opportunity.currency)}
          </Row>
          <Row label={t('summary.owner')} testId="summary-owner">
            {opportunity.assignedPrincipalSubject && (
              <div className="flex flex-wrap items-center gap-2">
                <span>{opportunity.assignedPrincipalDisplayName ?? '-'}</span>
                {canReassign && onReassign && <Button type="button" variant="ghost" size="sm" onClick={onReassign}>{t('summary.reassign')}</Button>}
              </div>
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
