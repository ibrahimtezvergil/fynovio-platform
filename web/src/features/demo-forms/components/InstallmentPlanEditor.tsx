import { CalendarClock, Plus, Trash2, Wand2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  DatePicker,
  InlineSelect,
  MoneyInput,
  NumberInput,
  formatMoney,
  roundMoney,
  type CurrencyCode,
} from '@/components/common/inputs'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type Interval = 'monthly' | 'biweekly'

interface Installment {
  id: string
  dueDate: Date | null
  amount: number | null
}

let sequence = 0
const nextId = () => `taksit-${(sequence += 1)}`

function addInterval(from: Date, index: number, interval: Interval): Date {
  const date = new Date(from)
  if (interval === 'monthly') date.setMonth(date.getMonth() + index)
  else date.setDate(date.getDate() + index * 14)
  return date
}

/**
 * A payment schedule generated from a total, then edited by hand.
 *
 * Generation is a starting point, not the answer: the customer will ask for a
 * bigger first instalment or a date moved past a holiday. What matters is that
 * the plan always closes on the total — so the remainder is shown at all times
 * and the rounding drift lands on one instalment rather than being spread as
 * kuruş across every row.
 */
export function InstallmentPlanEditor() {
  const { t } = useTranslation('demo-forms')
  const INTERVALS = [
    { value: 'monthly', label: t('installmentPlanEditor.intervals.monthly') },
    { value: 'biweekly', label: t('installmentPlanEditor.intervals.biweekly') },
  ] as const
  const [currency] = useState<CurrencyCode>('TRY')
  const [total, setTotal] = useState<number | null>(120000)
  const [count, setCount] = useState<number | null>(6)
  const [firstDate, setFirstDate] = useState<Date | null>(new Date())
  const [interval, setInterval] = useState<Interval>('monthly')
  const [rows, setRows] = useState<Installment[]>([])

  const planned = roundMoney(rows.reduce((sum, row) => sum + (row.amount ?? 0), 0))
  const remainder = roundMoney((total ?? 0) - planned)

  const generate = () => {
    const parts = Math.max(count ?? 0, 1)
    const start = firstDate ?? new Date()
    const share = roundMoney((total ?? 0) / parts)
    setRows(
      Array.from({ length: parts }, (_, index) => ({
        id: nextId(),
        dueDate: addInterval(start, index, interval),
        // The last instalment absorbs the rounding, so the plan closes exactly.
        amount:
          index === parts - 1 ? roundMoney((total ?? 0) - share * (parts - 1)) : share,
      })),
    )
  }

  const update = (id: string, patch: Partial<Installment>) =>
    setRows((current) => current.map((row) => (row.id === id ? { ...row, ...patch } : row)))

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Labelled label={t('installmentPlanEditor.labels.total')}>
          <MoneyInput aria-label={t('installmentPlanEditor.aria.total')} value={total} onValueChange={setTotal} />
        </Labelled>
        <Labelled label={t('installmentPlanEditor.labels.count')}>
          <NumberInput
            aria-label={t('installmentPlanEditor.aria.count')}
            value={count}
            onValueChange={setCount}
            min={1}
            max={36}
            steppers
          />
        </Labelled>
        <Labelled label={t('installmentPlanEditor.labels.firstDueDate')}>
          <DatePicker aria-label={t('installmentPlanEditor.aria.firstDueDate')} value={firstDate} onValueChange={setFirstDate} />
        </Labelled>
        <Labelled label={t('installmentPlanEditor.labels.frequency')}>
          <span className="flex h-control items-center overflow-hidden rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)]">
            <InlineSelect
              aria-label={t('installmentPlanEditor.aria.frequency')}
              value={interval}
              onChange={(next) => setInterval(next as Interval)}
              options={INTERVALS}
              className="w-full border-l-0 [&>select]:w-full"
            />
          </span>
        </Labelled>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="secondary" size="sm" onClick={generate}>
          <Wand2 aria-hidden />
          {t('installmentPlanEditor.generate')}
        </Button>
        {rows.length > 0 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() =>
              setRows((current) => [...current, { id: nextId(), dueDate: null, amount: null }])
            }
          >
            <Plus aria-hidden />
            {t('installmentPlanEditor.addInstallment')}
          </Button>
        )}
      </div>

      {rows.length === 0 ? (
        <p className="text-muted-foreground flex items-center gap-2 rounded-lg border border-dashed border-[var(--nx-hairline-strong)] px-4 py-6 text-[12.5px]">
          <CalendarClock aria-hidden className="size-4" strokeWidth={1.6} />
          {t('installmentPlanEditor.emptyState')}
        </p>
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {rows.map((row, index) => (
              <li
                key={row.id}
                className="grid grid-cols-[28px_minmax(0,1fr)_minmax(0,150px)_36px] items-center gap-2.5"
              >
                <span className="text-muted-foreground tnum text-[12px]">{index + 1}.</span>
                <DatePicker
                  aria-label={t('installmentPlanEditor.aria.rowDueDate', { index: index + 1 })}
                  value={row.dueDate}
                  onValueChange={(dueDate) => update(row.id, { dueDate })}
                  clearable={false}
                />
                <MoneyInput
                  aria-label={t('installmentPlanEditor.aria.rowAmount', { index: index + 1 })}
                  value={row.amount}
                  onValueChange={(amount) => update(row.id, { amount })}
                  currency={currency}
                />
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={t('installmentPlanEditor.aria.rowDelete', { index: index + 1 })}
                  onClick={() => setRows((current) => current.filter((entry) => entry.id !== row.id))}
                >
                  <Trash2 aria-hidden />
                </Button>
              </li>
            ))}
          </ul>

          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--nx-hairline)] pt-3">
            {remainder !== 0 ? (
              <button
                type="button"
                onClick={() =>
                  update(rows[rows.length - 1].id, {
                    amount: roundMoney((rows[rows.length - 1].amount ?? 0) + remainder),
                  })
                }
                className="text-accent-foreground cursor-pointer border-0 bg-transparent p-0 text-[11.5px] font-[550] underline-offset-4 outline-none hover:underline"
              >
                {t('installmentPlanEditor.addRemainder')}
              </button>
            ) : (
              <span className="text-[11.5px] text-[var(--nx-pos)]">{t('installmentPlanEditor.planClosed')}</span>
            )}
            <p aria-live="polite" className="tnum text-[12.5px]">
              <span className="text-muted-foreground">
                {t('installmentPlanEditor.summary.planned')} {formatMoney(planned, currency)} · {t('installmentPlanEditor.summary.difference')}{' '}
              </span>
              <span
                className={cn(
                  'font-[590]',
                  remainder === 0 ? 'text-[var(--nx-pos)]' : 'text-[var(--nx-neg)]',
                )}
              >
                {formatMoney(remainder, currency)}
              </span>
            </p>
          </div>
        </>
      )}
    </div>
  )
}

function Labelled({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-muted-foreground text-[12.5px] font-[550]">{label}</span>
      {children}
    </div>
  )
}
