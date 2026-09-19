import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { StatusBadge } from '@/components/common/StatusBadge'
import { DetailDrawer } from '@/features/demo-drawers/components/DetailDrawer'
import { EditDrawer } from '@/features/demo-drawers/components/EditDrawer'
import {
  CUSTOMERS,
  CUSTOMER_STATUS,
  money,
  type CustomerRecord,
} from '@/features/demo-drawers/data/customers'
import { initialsOf } from '@/lib/utils'

/**
 * The list and its panel.
 *
 * The selection is an *index*, not a record: prev/next only mean something
 * relative to the list the reader clicked from, and an id would have to look
 * its own position up again on every step.
 */
export function RecordExplorer() {
  const { t } = useTranslation('demo-drawers')
  const [records, setRecords] = useState<CustomerRecord[]>(CUSTOMERS)
  const [selected, setSelected] = useState<number | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)

  const record = selected != null ? (records[selected] ?? null) : null

  const open = (index: number) => {
    setSelected(index)
    setDetailOpen(true)
  }

  return (
    <>
      <div className="overflow-x-auto rounded-lg border border-[var(--nx-hairline)]">
        <table className="nx-grid [&_td]:whitespace-nowrap">
          <caption className="sr-only">{t('recordExplorer.caption')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('recordExplorer.headers.account')}</th>
              <th scope="col">{t('recordExplorer.headers.owner')}</th>
              <th scope="col">{t('recordExplorer.headers.city')}</th>
              <th scope="col" className="num">
                {t('recordExplorer.headers.openDeals')}
              </th>
              <th scope="col" className="num">
                {t('recordExplorer.headers.lifetimeValue')}
              </th>
              <th scope="col" className="end">
                {t('recordExplorer.headers.status')}
              </th>
            </tr>
          </thead>
          <tbody>
            {records.map((entry, index) => (
              <tr
                key={entry.id}
                data-selected={detailOpen && selected === index ? '' : undefined}
                className="cursor-pointer"
                onClick={() => open(index)}
              >
                <td className="name">
                  {/* The button carries the accessible action; the row click is
                      the convenience layer on top of it, not a substitute. */}
                  <span className="flex items-center gap-2.5">
                    <span aria-hidden className="nx-avatar size-7">
                      {initialsOf(entry.name)}
                    </span>
                    <button
                      type="button"
                      onClick={(event) => {
                        event.stopPropagation()
                        open(index)
                      }}
                      className="text-left hover:underline"
                    >
                      {entry.name}
                      <span className="sr-only"> {t('recordExplorer.openDetail')}</span>
                    </button>
                  </span>
                </td>
                <td>{entry.owner}</td>
                <td>{entry.city}</td>
                <td className="num">{entry.openDeals}</td>
                <td className="num">{money.format(entry.lifetimeValue)}</td>
                <td className="end">
                  <StatusBadge {...CUSTOMER_STATUS[entry.status]} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <DetailDrawer
        record={record}
        open={detailOpen}
        onOpenChange={setDetailOpen}
        hasPrev={selected != null && selected > 0}
        hasNext={selected != null && selected < records.length - 1}
        onPrev={() => setSelected((index) => (index == null ? index : Math.max(0, index - 1)))}
        onNext={() =>
          setSelected((index) =>
            index == null ? index : Math.min(records.length - 1, index + 1),
          )
        }
        position={selected != null ? `${selected + 1} / ${records.length}` : ''}
        onEdit={() => {
          setDetailOpen(false)
          setEditOpen(true)
        }}
      />

      <EditDrawer
        record={record}
        open={editOpen}
        onOpenChange={setEditOpen}
        onSave={(values) =>
          setRecords((current) =>
            current.map((entry, index) => (index === selected ? { ...entry, ...values } : entry)),
          )
        }
      />
    </>
  )
}
