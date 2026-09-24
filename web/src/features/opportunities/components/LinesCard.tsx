import { CircleAlert, Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatMoney } from '../lib/format'
import { lineEditability } from '../lib/lineEditability'
import type { Opportunity, OpportunityLine } from '../schema'
import { AddLineDialog, CancelLineDialog } from './LineDialogs'

/**
 * Lines are the only editable CRM-owned state Phase 2 defines (there is no update command for scalar fields),
 * so this card IS the edit surface. Which controls to offer comes from `lineEditability` (integration gap G4).
 */
export function LinesCard({ opportunity, onReload }: { opportunity: Opportunity; onReload: () => void }) {
  const { t } = useTranslation('opportunities')
  const { canOfferAdd, canOfferCancel } = lineEditability(opportunity)
  const hasActiveRequiredLine = opportunity.lines.some((line) => !line.isCanceled && !line.isOptional)
  const [adding, setAdding] = useState(false)
  const [cancelling, setCancelling] = useState<OpportunityLine | null>(null)

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('lines.title')}</CardTitle>
        <CardDescription>{t('lines.description')}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3">
        {opportunity.lines.length === 0 ? (
          <p className="text-muted-foreground text-[13px]">{t('lines.empty')}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-[13px]">
              <caption className="sr-only">{t('lines.title')}</caption>
              <thead>
                <tr className="text-muted-foreground border-b text-left text-[12px]">
                  <th className="py-2 pr-3 font-[550]">{t('lines.columns.id')}</th>
                  <th className="py-2 pr-3 text-right font-[550]">{t('lines.columns.quantity')}</th>
                  <th className="py-2 pr-3 text-right font-[550]">{t('lines.columns.unitPrice')}</th>
                  <th className="py-2 pr-3 text-right font-[550]">{t('lines.columns.total')}</th>
                  <th className="py-2 pr-3 font-[550]">{t('lines.columns.kind')}</th>
                  <th className="py-2 font-[550]">
                    <span className="sr-only">{t('lines.columns.actions')}</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {opportunity.lines.map((line) => (
                  <tr key={line.id} data-canceled={line.isCanceled} className="border-b last:border-b-0 data-[canceled=true]:opacity-55">
                    <td className="py-2 pr-3 tabular-nums">#{line.id}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{line.quantity}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{formatMoney(line.unitPrice, opportunity.currency)}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{formatMoney(line.lineTotal ?? line.quantity * line.unitPrice, opportunity.currency)}</td>
                    <td className="py-2 pr-3">
                      {line.isCanceled ? t('lines.kind.canceled') : line.isOptional ? t('lines.kind.optional') : t('lines.kind.required')}
                    </td>
                    <td className="py-2 text-right">
                      {canOfferCancel && !line.isCanceled && (
                        <Button type="button" variant="ghost" size="sm" onClick={() => setCancelling(line)}>
                          {t('lines.cancel.action')}
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {canOfferAdd && !hasActiveRequiredLine && (
          <Alert variant="warning">
            <CircleAlert aria-hidden />
            <AlertTitle>{t('lines.requiredNotice.title')}</AlertTitle>
            <AlertDescription>{t('lines.requiredNotice.description')}</AlertDescription>
          </Alert>
        )}
        {canOfferAdd && (
          <div>
            <Button type="button" variant={hasActiveRequiredLine ? 'outline' : 'default'} size="sm" onClick={() => setAdding(true)}>
              <Plus aria-hidden />
              {hasActiveRequiredLine ? t('lines.add.action') : t('lines.add.firstAction')}
            </Button>
          </div>
        )}
      </CardContent>
      {adding && <AddLineDialog opportunity={opportunity} onClose={() => setAdding(false)} onReload={onReload} />}
      {cancelling && <CancelLineDialog opportunity={opportunity} line={cancelling} onClose={() => setCancelling(null)} onReload={onReload} />}
    </Card>
  )
}
