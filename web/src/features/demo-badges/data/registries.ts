import {
  CircleCheck,
  CircleSlash,
  CircleX,
  Clock,
  FileText,
  type LucideIcon,
  LoaderCircle,
  SquarePen,
  TriangleAlert,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useStageMeta } from '@/components/common/StageBadge'
import type { StatusMeta, StatusRegistry, StatusTone } from '@/components/common/StatusBadge'
import { i18n } from '@/lib/i18n'
import { STAGES } from '@/types'

/* -------------------------------------------------------------------------
   Domain vocabularies.

   A status set is defined once, in the order it is displayed, and everything
   that draws it — badge, filter, chart legend, kanban column — reads the same
   record. The alternative is a second `switch` per screen, and two screens
   disagreeing about whether "İptal" is red or grey.
   ------------------------------------------------------------------------- */

export const QUOTE_STATUSES = ['draft', 'sent', 'approved', 'rejected', 'expired', 'cancelled'] as const
export type QuoteStatus = (typeof QUOTE_STATUSES)[number]
const QUOTE_TONE: Record<QuoteStatus, StatusTone> = {
  draft: 'gray',
  sent: 'blue',
  approved: 'green',
  rejected: 'red',
  expired: 'amber',
  cancelled: 'gray',
}

export const INVOICE_STATUSES = ['draft', 'pending', 'partial', 'paid', 'overdue', 'refunded'] as const
export type InvoiceStatus = (typeof INVOICE_STATUSES)[number]
const INVOICE_TONE: Record<InvoiceStatus, StatusTone> = {
  draft: 'gray',
  pending: 'amber',
  partial: 'teal',
  paid: 'green',
  overdue: 'red',
  refunded: 'purple',
}

export const SHIPMENT_STATUSES = ['preparing', 'packed', 'shipped', 'transit', 'delivered', 'returned'] as const
export type ShipmentStatus = (typeof SHIPMENT_STATUSES)[number]
const SHIPMENT_TONE: Record<ShipmentStatus, StatusTone> = {
  preparing: 'gray',
  packed: 'blue',
  shipped: 'teal',
  transit: 'purple',
  delivered: 'green',
  returned: 'red',
}

export const STOCK_STATUSES = ['in_stock', 'low', 'critical', 'out', 'discontinued'] as const
export type StockStatus = (typeof STOCK_STATUSES)[number]
const STOCK_TONE: Record<StockStatus, StatusTone> = {
  in_stock: 'green',
  low: 'amber',
  critical: 'red',
  out: 'gray',
  discontinued: 'outline',
}

/** The one vocabulary where the mark adds meaning: an approval is a verdict. */
export const APPROVAL_STATUSES = ['draft', 'waiting', 'approved', 'rejected', 'revision'] as const
export type ApprovalStatus = (typeof APPROVAL_STATUSES)[number]
const APPROVAL_TONE: Record<ApprovalStatus, StatusTone> = {
  draft: 'gray',
  waiting: 'amber',
  approved: 'green',
  rejected: 'red',
  revision: 'purple',
}
const APPROVAL_ICON: Record<ApprovalStatus, LucideIcon> = {
  draft: FileText,
  waiting: Clock,
  approved: CircleCheck,
  rejected: CircleX,
  revision: SquarePen,
}

export const SYNC_STATUSES = ['synced', 'syncing', 'failed', 'paused'] as const
export type SyncStatus = (typeof SYNC_STATUSES)[number]
const SYNC_TONE: Record<SyncStatus, StatusTone> = {
  synced: 'green',
  syncing: 'blue',
  failed: 'red',
  paused: 'gray',
}
const SYNC_ICON: Record<SyncStatus, LucideIcon> = {
  synced: CircleCheck,
  syncing: LoaderCircle,
  failed: TriangleAlert,
  paused: CircleSlash,
}

export const MEMBER_STATUSES = ['active', 'invited', 'suspended', 'archived'] as const
export type MemberStatus = (typeof MEMBER_STATUSES)[number]
const MEMBER_TONE: Record<MemberStatus, StatusTone> = {
  active: 'green',
  invited: 'blue',
  suspended: 'amber',
  archived: 'gray',
}

type Translator = (key: string) => string

function buildStatus<T extends string>(
  t: Translator,
  group: string,
  keys: readonly T[],
  tones: Record<T, StatusTone>,
  icons?: Partial<Record<T, LucideIcon>>,
): StatusRegistry<T> {
  return keys.reduce((registry, key) => {
    registry[key] = { label: t(`${group}.${key}`), tone: tones[key], icon: icons?.[key] }
    return registry
  }, {} as StatusRegistry<T>)
}

const staticT: Translator = (key) => i18n.t(key, { ns: 'demo-badges' })

/** Resolved against the active language at call time — see `stageMeta()` for why. */
export const quoteStatus = (): StatusRegistry<QuoteStatus> =>
  buildStatus(staticT, 'quote', QUOTE_STATUSES, QUOTE_TONE)
export const invoiceStatus = (): StatusRegistry<InvoiceStatus> =>
  buildStatus(staticT, 'invoice', INVOICE_STATUSES, INVOICE_TONE)
export const shipmentStatus = (): StatusRegistry<ShipmentStatus> =>
  buildStatus(staticT, 'shipment', SHIPMENT_STATUSES, SHIPMENT_TONE)
export const stockStatus = (): StatusRegistry<StockStatus> =>
  buildStatus(staticT, 'stock', STOCK_STATUSES, STOCK_TONE)
export const approvalStatus = (): StatusRegistry<ApprovalStatus> =>
  buildStatus(staticT, 'approval', APPROVAL_STATUSES, APPROVAL_TONE, APPROVAL_ICON)
export const syncStatus = (): StatusRegistry<SyncStatus> =>
  buildStatus(staticT, 'sync', SYNC_STATUSES, SYNC_TONE, SYNC_ICON)
export const memberStatus = (): StatusRegistry<MemberStatus> =>
  buildStatus(staticT, 'member', MEMBER_STATUSES, MEMBER_TONE)

/** The reactive form of every status registry above — re-renders on a language change. */
export function useStatusRegistries() {
  const { t } = useTranslation('demo-badges')
  return {
    quote: buildStatus(t, 'quote', QUOTE_STATUSES, QUOTE_TONE),
    invoice: buildStatus(t, 'invoice', INVOICE_STATUSES, INVOICE_TONE),
    shipment: buildStatus(t, 'shipment', SHIPMENT_STATUSES, SHIPMENT_TONE),
    stock: buildStatus(t, 'stock', STOCK_STATUSES, STOCK_TONE),
    approval: buildStatus(t, 'approval', APPROVAL_STATUSES, APPROVAL_TONE, APPROVAL_ICON),
    sync: buildStatus(t, 'sync', SYNC_STATUSES, SYNC_TONE, SYNC_ICON),
    member: buildStatus(t, 'member', MEMBER_STATUSES, MEMBER_TONE),
  }
}

/* ---- the page's index of everything above ------------------------------- */

export interface Vocabulary {
  id: string
  label: string
  description: string
  entries: readonly (StatusMeta & { key: string })[]
}

function listOf<T extends string>(
  order: readonly T[],
  registry: StatusRegistry<T>,
): (StatusMeta & { key: string })[] {
  return order.map((key) => ({ key, ...registry[key] }))
}

/** The reactive form — re-renders on a language change. */
export function useVocabularies(): Vocabulary[] {
  const { t } = useTranslation('demo-badges')
  const stage = useStageMeta()
  const status = useStatusRegistries()
  return [
    {
      id: 'pipeline',
      label: t('vocabulary.pipeline.label'),
      description: t('vocabulary.pipeline.description'),
      entries: listOf(STAGES, stage),
    },
    {
      id: 'quote',
      label: t('vocabulary.quote.label'),
      description: t('vocabulary.quote.description'),
      entries: listOf(QUOTE_STATUSES, status.quote),
    },
    {
      id: 'invoice',
      label: t('vocabulary.invoice.label'),
      description: t('vocabulary.invoice.description'),
      entries: listOf(INVOICE_STATUSES, status.invoice),
    },
    {
      id: 'shipment',
      label: t('vocabulary.shipment.label'),
      description: t('vocabulary.shipment.description'),
      entries: listOf(SHIPMENT_STATUSES, status.shipment),
    },
    {
      id: 'stock',
      label: t('vocabulary.stock.label'),
      description: t('vocabulary.stock.description'),
      entries: listOf(STOCK_STATUSES, status.stock),
    },
    {
      id: 'approval',
      label: t('vocabulary.approval.label'),
      description: t('vocabulary.approval.description'),
      entries: listOf(APPROVAL_STATUSES, status.approval),
    },
    {
      id: 'sync',
      label: t('vocabulary.sync.label'),
      description: t('vocabulary.sync.description'),
      entries: listOf(SYNC_STATUSES, status.sync),
    },
    {
      id: 'member',
      label: t('vocabulary.member.label'),
      description: t('vocabulary.member.description'),
      entries: listOf(MEMBER_STATUSES, status.member),
    },
  ]
}

/** What each hue is allowed to mean, before any domain gets to use it. */
const TONES: readonly StatusTone[] = ['green', 'blue', 'teal', 'purple', 'amber', 'red', 'gray', 'outline']

export function useToneSemantics(): readonly (readonly [StatusTone, string, string])[] {
  const { t } = useTranslation('demo-badges')
  return TONES.map((tone) => [tone, t(`tone.${tone}.meaning`), t(`tone.${tone}.examples`)] as const)
}
