import { ChevronLeft, ChevronRight, ExternalLink, Mail, Paperclip, Phone } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { CUSTOMER_STATUS, money, type CustomerRecord } from '@/features/demo-drawers/data/customers'
import { initialsOf } from '@/lib/utils'

type Tab = 'ozet' | 'kisiler' | 'belgeler' | 'hareketler'

interface DetailDrawerProps {
  record: CustomerRecord | null
  open: boolean
  onOpenChange: (open: boolean) => void
  onPrev: () => void
  onNext: () => void
  hasPrev: boolean
  hasNext: boolean
  /** "2 / 5" — the panel says where in the list the reader is standing. */
  position: string
  onEdit: () => void
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-[var(--nx-hairline-soft)] py-2.5 last:border-b-0">
      <dt className="text-muted-foreground shrink-0 text-[12px]">{label}</dt>
      <dd className="min-w-0 text-right text-[12.5px] font-[550]">{children}</dd>
    </div>
  )
}

/**
 * The row-detail panel: everything about one record, without leaving the list.
 *
 * Three things make it a detail *drawer* rather than a modal that happens to
 * slide: the list stays visible behind it, the header says which record of how
 * many this is, and prev/next move through the same list the reader clicked
 * from. Take any of the three away and a full page would have been the honest
 * choice.
 */
export function DetailDrawer({
  record,
  open,
  onOpenChange,
  onPrev,
  onNext,
  hasPrev,
  hasNext,
  position,
  onEdit,
}: DetailDrawerProps) {
  const { t } = useTranslation('demo-drawers')
  const [tab, setTab] = useState<Tab>('ozet')

  const TABS: readonly Segment<Tab>[] = [
    { value: 'ozet', label: t('detailDrawer.tabs.summary') },
    { value: 'kisiler', label: t('detailDrawer.tabs.contacts') },
    { value: 'belgeler', label: t('detailDrawer.tabs.documents') },
    { value: 'hareketler', label: t('detailDrawer.tabs.history') },
  ]

  if (!record) return null

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="gap-0 sm:max-w-xl">
        <SheetHeader className="gap-2.5">
          <div className="flex items-center gap-1.5">
            <Button
              variant="ghost"
              size="icon-sm"
              disabled={!hasPrev}
              aria-label={t('detailDrawer.prevRecord')}
              onClick={onPrev}
            >
              <ChevronLeft strokeWidth={1.8} />
            </Button>
            <Button
              variant="ghost"
              size="icon-sm"
              disabled={!hasNext}
              aria-label={t('detailDrawer.nextRecord')}
              onClick={onNext}
            >
              <ChevronRight strokeWidth={1.8} />
            </Button>
            <span className="text-[var(--nx-label-3)] tnum text-[11.5px]">{position}</span>
            <span className="text-[var(--nx-label-3)] ml-auto mr-1 font-mono text-[11px]">
              {record.id}
            </span>
          </div>

          <div className="flex items-start gap-3">
            <span aria-hidden className="nx-avatar size-11 text-[14px]">
              {initialsOf(record.name)}
            </span>
            <div className="flex min-w-0 flex-col gap-1">
              <SheetTitle>{record.name}</SheetTitle>
              <SheetDescription>
                {t('detailDrawer.identityLine', {
                  segment: record.segment,
                  city: record.city,
                  owner: record.owner,
                })}
              </SheetDescription>
              <StatusBadge {...CUSTOMER_STATUS[record.status]} className="mt-0.5 w-fit" />
            </div>
          </div>

          <div className="flex flex-wrap gap-2 pt-1">
            <Button size="sm" variant="outline">
              <Mail strokeWidth={1.75} />
              {t('detailDrawer.emailAction')}
            </Button>
            <Button size="sm" variant="outline">
              <Phone strokeWidth={1.75} />
              {t('detailDrawer.callAction')}
            </Button>
            <Button size="sm" variant="outline">
              <ExternalLink strokeWidth={1.75} />
              {t('detailDrawer.openFullPage')}
            </Button>
          </div>
        </SheetHeader>

        <div className="px-5 pt-4 pb-3">
          <SegmentedControl
            aria-label={t('detailDrawer.tabsAria')}
            segments={TABS}
            value={tab}
            onChange={setTab}
            fullWidth
          />
        </div>

        <div className="flex-1 overflow-y-auto border-t border-[var(--nx-hairline)] px-5 py-4">
          {tab === 'ozet' && (
            <div className="flex flex-col gap-4">
              <dl className="flex flex-col">
                <Row label={t('detailDrawer.fields.email')}>{record.email}</Row>
                <Row label={t('detailDrawer.fields.phone')}>
                  <span className="tnum">{record.phone}</span>
                </Row>
                <Row label={t('detailDrawer.fields.taxId')}>
                  <span className="tnum font-mono text-[12px]">{record.taxId}</span>
                </Row>
                <Row label={t('detailDrawer.fields.paymentTerm')}>{record.paymentTerm}</Row>
                <Row label={t('detailDrawer.fields.openDeals')}>
                  <span className="tnum">{record.openDeals}</span>
                </Row>
                <Row label={t('detailDrawer.fields.lifetimeValue')}>
                  <span className="tnum">{money.format(record.lifetimeValue)}</span>
                </Row>
                <Row label={t('detailDrawer.fields.lastContact')}>{record.lastContact}</Row>
              </dl>

              <div className="rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-3">
                <p className="nx-eyebrow mb-1.5">{t('detailDrawer.accountNote')}</p>
                <p className="text-muted-foreground text-[12.5px] leading-[1.5]">{record.note}</p>
              </div>
            </div>
          )}

          {tab === 'kisiler' && (
            <ul className="flex flex-col">
              {record.contacts.map((contact) => (
                <li key={contact.email} className="nx-row px-0">
                  <span aria-hidden className="nx-avatar size-8">
                    {initialsOf(contact.name)}
                  </span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="text-[13px] font-[550]">{contact.name}</span>
                    <span className="text-muted-foreground truncate text-[11.5px]">
                      {contact.role} · {contact.email}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}

          {tab === 'belgeler' && (
            <ul className="flex flex-col">
              {record.documents.map((document) => (
                <li key={document.name} className="nx-row px-0">
                  <span aria-hidden className="nx-icon-tile" data-tone="blue">
                    <Paperclip className="size-4" strokeWidth={1.75} />
                  </span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-[13px] font-[550]">{document.name}</span>
                    <span className="text-muted-foreground tnum text-[11.5px]">
                      {document.size} · {document.date}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}

          {tab === 'hareketler' && (
            <ol className="flex flex-col">
              {record.history.map((event) => (
                <li
                  key={event.title + event.time}
                  className="flex gap-3 border-b border-[var(--nx-hairline-soft)] py-3 last:border-b-0"
                >
                  <span className="text-muted-foreground tnum w-[76px] shrink-0 text-right text-[11px]">
                    {event.time}
                  </span>
                  <span className="min-w-0">
                    <span className="block text-[12.5px] font-[550]">{event.title}</span>
                    <span className="text-muted-foreground block text-[11.5px]">{event.note}</span>
                  </span>
                </li>
              ))}
            </ol>
          )}
        </div>

        <SheetFooter>
          <SheetClose render={<Button variant="outline" />}>{t('detailDrawer.close')}</SheetClose>
          <Button onClick={onEdit}>{t('detailDrawer.edit')}</Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}
