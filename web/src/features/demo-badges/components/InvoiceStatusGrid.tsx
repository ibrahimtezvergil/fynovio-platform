import { ChevronDown } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  INVOICE_STATUSES,
  useStatusRegistries,
  type InvoiceStatus,
} from '@/features/demo-badges/data/registries'

interface InvoiceRow {
  id: string
  account: string
  amount: number
  status: InvoiceStatus
}

const SEED: InvoiceRow[] = [
  { id: 'FT-2026-0841', account: 'Nordwind Lojistik', amount: 184_500, status: 'paid' },
  { id: 'FT-2026-0839', account: 'Baltic Freight AB', amount: 96_200, status: 'overdue' },
  { id: 'FT-2026-0844', account: 'Meridian Retail Group', amount: 312_000, status: 'pending' },
  { id: 'FT-2026-0846', account: 'Ege Yapı Malzeme', amount: 58_400, status: 'partial' },
  { id: 'FT-2026-0850', account: 'Levant Trade Co.', amount: 24_900, status: 'draft' },
  { id: 'FT-2026-0812', account: 'Harborline Shipping', amount: 41_600, status: 'refunded' },
]

const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})

/**
 * The badge where it actually earns its keep: a status column in a grid.
 *
 * It is right-aligned with the numbers rather than left with the names —
 * that is the grid's alignment contract, and it also puts every pill on one
 * vertical edge, which is what makes a column of them scannable at a glance
 * instead of a ragged staircase.
 */
export function InvoiceStatusGrid() {
  const { t } = useTranslation('demo-badges')
  const status = useStatusRegistries()
  const [rows, setRows] = useState(SEED)

  const setStatus = (id: string, next: InvoiceStatus) =>
    setRows((current) => current.map((row) => (row.id === id ? { ...row, status: next } : row)))

  return (
    <div className="overflow-hidden rounded-lg border border-[var(--nx-hairline)]">
      <table className="nx-grid [&_td]:whitespace-nowrap">
        <caption className="sr-only">{t('grid.caption')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('grid.invoiceHeader')}</th>
            <th scope="col">{t('grid.accountHeader')}</th>
            <th scope="col" className="end">
              {t('grid.dueHeader')}
            </th>
            <th scope="col" className="num">
              {t('grid.amountHeader')}
            </th>
            <th scope="col" className="end">
              {t('grid.statusHeader')}
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.id}>
              <td className="name font-mono text-[12.5px]">{row.id}</td>
              <td className="name">{row.account}</td>
              <td className="end tnum">{t(`grid.due.${row.id}`)}</td>
              <td className="num">{money.format(row.amount)}</td>
              <td className="end">
                <DropdownMenu>
                  <DropdownMenuTrigger
                    render={
                      <Button
                        variant="ghost"
                        size="sm"
                        className="px-1.5"
                        aria-label={t('grid.changeAriaLabel', { id: row.id, label: status.invoice[row.status].label })}
                      >
                        <span className="sr-only">{t('grid.changeAriaLabel', { id: row.id, label: status.invoice[row.status].label })}</span>
                      </Button>
                    }
                  >
                    <StatusBadge {...status.invoice[row.status]} />
                    <ChevronDown aria-hidden strokeWidth={1.7} />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="w-56">
                    <DropdownMenuLabel>{t('grid.menuLabel')}</DropdownMenuLabel>
                    <DropdownMenuSeparator />
                    <DropdownMenuRadioGroup
                      value={row.status}
                      onValueChange={(next) => setStatus(row.id, next as InvoiceStatus)}
                    >
                      {INVOICE_STATUSES.map((option) => (
                        <DropdownMenuRadioItem key={option} value={option}>
                          <StatusBadge {...status.invoice[option]} />
                        </DropdownMenuRadioItem>
                      ))}
                    </DropdownMenuRadioGroup>
                  </DropdownMenuContent>
                </DropdownMenu>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
