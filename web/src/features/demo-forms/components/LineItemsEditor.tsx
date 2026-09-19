import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ComboboxInput,
  CURRENCY_CODES,
  DiscountInput,
  InlineSelect,
  MoneyInput,
  QuantityInput,
  formatMoney,
  resolveDiscount,
  roundMoney,
  type CurrencyCode,
  type Discount,
  type Unit,
} from '@/components/common/inputs'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import {
  CATALOGUE,
  CATALOGUE_OPTIONS,
  VAT_RATES,
  type VatRate,
} from '@/features/demo-forms/data/options'

interface Line {
  id: string
  code: string | null
  quantity: number | null
  unit: Unit
  unitPrice: number | null
  discount: Discount
  vatRate: VatRate
}

/** Gross, discount, net and VAT for one line — every figure derived, none stored. */
function lineTotals(line: Line) {
  const gross = roundMoney((line.quantity ?? 0) * (line.unitPrice ?? 0))
  const discount = resolveDiscount(gross, line.discount)
  const net = roundMoney(gross - discount)
  const vat = roundMoney((net * line.vatRate) / 100)
  return { gross, discount, net, vat, total: roundMoney(net + vat) }
}

let sequence = 0
const newLine = (): Line => ({
  id: `line-${(sequence += 1)}`,
  code: null,
  quantity: 1,
  unit: 'adet',
  unitPrice: null,
  discount: { mode: 'percent', value: null },
  vatRate: 20,
})

const SEEDED: Line[] = [
  {
    id: 'line-0',
    code: 'LIC-CRM-01',
    quantity: 25,
    unit: 'adet',
    unitPrice: 4800,
    discount: { mode: 'percent', value: 10 },
    vatRate: 20,
  },
  {
    id: 'line-1',
    code: 'SRV-KUR-01',
    quantity: 16,
    unit: 'saat',
    unitPrice: 1750,
    discount: { mode: 'amount', value: 2000 },
    vatRate: 20,
  },
]

const GRID =
  'grid min-w-[1010px] grid-cols-[minmax(180px,2fr)_176px_150px_175px_88px_132px_36px] items-start gap-2.5'

/**
 * The composite the numeric controls exist for: a quote's line table.
 *
 * Nothing here is stored twice. Quantity, unit price, discount and VAT rate are
 * the only inputs; every gross, net, tax and total on screen is derived from
 * them on render, which is why the footer can never disagree with the rows.
 */
export function LineItemsEditor() {
  const { t } = useTranslation('demo-forms')
  const [currency, setCurrency] = useState<CurrencyCode>('TRY')
  const [lines, setLines] = useState<Line[]>(SEEDED)
  const [globalDiscount, setGlobalDiscount] = useState<Discount>({ mode: 'percent', value: null })

  const update = (id: string, patch: Partial<Line>) =>
    setLines((current) => current.map((line) => (line.id === id ? { ...line, ...patch } : line)))

  /** Picking a catalogue item carries its price, unit and VAT band with it. */
  const applyCatalogue = (id: string, code: string | null) => {
    const item = CATALOGUE.find((entry) => entry.code === code)
    update(id, {
      code,
      ...(item ? { unitPrice: item.unitPrice, unit: item.unit, vatRate: item.vatRate } : {}),
    })
  }

  const totals = lines.reduce(
    (accumulator, line) => {
      const line_ = lineTotals(line)
      return {
        gross: roundMoney(accumulator.gross + line_.gross),
        discount: roundMoney(accumulator.discount + line_.discount),
        net: roundMoney(accumulator.net + line_.net),
      }
    },
    { gross: 0, discount: 0, net: 0 },
  )

  const globalDiscountAmount = resolveDiscount(totals.net, globalDiscount)
  const netAfterGlobal = roundMoney(totals.net - globalDiscountAmount)
  // The document discount lands on each line in proportion, so every VAT band
  // is reduced by its own share instead of all of it hitting the highest rate.
  const share = totals.net > 0 ? netAfterGlobal / totals.net : 1

  const vatByRate = lines.reduce<Record<number, number>>((accumulator, line) => {
    const { net } = lineTotals(line)
    accumulator[line.vatRate] = roundMoney(
      (accumulator[line.vatRate] ?? 0) + roundMoney((net * share * line.vatRate) / 100),
    )
    return accumulator
  }, {})

  const vatTotal = roundMoney(
    Object.values(vatByRate).reduce((sum, amount) => sum + amount, 0),
  )
  const grandTotal = roundMoney(netAfterGlobal + vatTotal)

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-[12.5px]">
          {t('lineItemsEditor.summary', { count: lines.length, currency })}
        </p>
        <div className="flex h-control items-center overflow-hidden rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)]">
          <span className="text-muted-foreground px-3 text-[12.5px] font-[550]">{t('lineItemsEditor.currencyLabel')}</span>
          <InlineSelect
            aria-label={t('lineItemsEditor.aria.documentCurrency')}
            value={currency}
            onChange={(next) => setCurrency(next as CurrencyCode)}
            options={CURRENCY_CODES.map((code) => ({ value: code, label: code }))}
          />
        </div>
      </div>

      <div className="overflow-x-auto pb-1">
        <div className={GRID}>
          {[
            t('lineItemsEditor.columns.product'),
            t('lineItemsEditor.columns.quantity'),
            t('lineItemsEditor.columns.unitPrice'),
            t('lineItemsEditor.columns.discount'),
            t('lineItemsEditor.columns.vat'),
            t('lineItemsEditor.columns.lineTotal'),
            '',
          ].map(
            (heading, index) => (
              <span
                key={heading || index}
                className={cn(
                  'text-[10.5px] font-[650] tracking-[0.06em] text-[var(--nx-label-3)] uppercase',
                  index >= 1 && index <= 5 && 'text-right',
                )}
              >
                {heading}
              </span>
            ),
          )}
        </div>

        <div className="mt-2 flex flex-col gap-2.5">
          {lines.map((line) => {
            const computed = lineTotals(line)
            return (
              <div key={line.id} className={GRID}>
                <ComboboxInput
                  aria-label={t('lineItemsEditor.aria.product')}
                  value={line.code}
                  onValueChange={(code) => applyCatalogue(line.id, code)}
                  options={CATALOGUE_OPTIONS}
                  placeholder={t('lineItemsEditor.placeholder')}
                />
                {/* The unit rides in from the catalogue row, so here it is a
                    label rather than a second thing to keep in sync. */}
                <QuantityInput
                  aria-label={t('lineItemsEditor.aria.quantity')}
                  value={line.quantity}
                  onValueChange={(quantity) => update(line.id, { quantity })}
                  unit={line.unit}
                />
                <MoneyInput
                  aria-label={t('lineItemsEditor.aria.unitPrice')}
                  value={line.unitPrice}
                  onValueChange={(unitPrice) => update(line.id, { unitPrice })}
                  currency={currency}
                />
                <DiscountInput
                  aria-label={t('lineItemsEditor.aria.discount')}
                  discount={line.discount}
                  onDiscountChange={(discount) => update(line.id, { discount })}
                  currency={currency}
                  base={computed.gross}
                  showResolved={false}
                />
                <InlineSelect
                  aria-label={t('lineItemsEditor.aria.vatRate')}
                  value={String(line.vatRate)}
                  onChange={(rate) => update(line.id, { vatRate: Number(rate) as VatRate })}
                  options={VAT_RATES.map((rate) => ({ value: String(rate), label: `%${rate}` }))}
                  className="h-control justify-end rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)]"
                />
                <span className="tnum flex h-control items-center justify-end text-[13.5px] font-[590]">
                  {formatMoney(computed.total, currency)}
                </span>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={t('lineItemsEditor.aria.deleteRow')}
                  disabled={lines.length === 1}
                  onClick={() => setLines((current) => current.filter((entry) => entry.id !== line.id))}
                  className="mt-1"
                >
                  <Trash2 aria-hidden />
                </Button>
              </div>
            )
          })}
        </div>
      </div>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => setLines((current) => [...current, newLine()])}
        >
          <Plus aria-hidden />
          {t('lineItemsEditor.addRow')}
        </Button>

        <div className="flex w-full max-w-sm flex-col gap-2">
          <Row label={t('lineItemsEditor.rows.subtotal')} value={formatMoney(totals.gross, currency)} />
          <Row label={t('lineItemsEditor.rows.lineDiscounts')} value={`− ${formatMoney(totals.discount, currency)}`} />

          <div className="grid grid-cols-[1fr_auto] items-center gap-3 py-1">
            <span className="text-muted-foreground text-[12.5px]">{t('lineItemsEditor.rows.globalDiscount')}</span>
            <DiscountInput
              aria-label={t('lineItemsEditor.aria.globalDiscount')}
              discount={globalDiscount}
              onDiscountChange={setGlobalDiscount}
              currency={currency}
              className="w-[190px]"
            />
          </div>
          {globalDiscountAmount > 0 && (
            <Row
              label={t('lineItemsEditor.rows.globalDiscountAmount')}
              value={`− ${formatMoney(globalDiscountAmount, currency)}`}
            />
          )}

          <Row label={t('lineItemsEditor.rows.taxBase')} value={formatMoney(netAfterGlobal, currency)} />
          {Object.entries(vatByRate)
            .sort(([a], [b]) => Number(a) - Number(b))
            .map(([rate, amount]) => (
              <Row key={rate} label={t('lineItemsEditor.rows.vatAtRate', { rate })} value={formatMoney(amount, currency)} muted />
            ))}

          <div className="mt-1 flex items-baseline justify-between gap-3 border-t border-[var(--nx-hairline)] pt-2.5">
            <span className="text-[13.5px] font-[590]">{t('lineItemsEditor.rows.grandTotal')}</span>
            <span className="tnum font-heading text-[19px] font-[620] tracking-[-0.02em]">
              {formatMoney(grandTotal, currency)}
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}

function Row({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <span className={cn('text-[12.5px]', muted ? 'text-[var(--nx-label-3)]' : 'text-muted-foreground')}>
        {label}
      </span>
      <span className="tnum text-[13px] font-[550]">{value}</span>
    </div>
  )
}
