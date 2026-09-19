import { useTranslation } from 'react-i18next'
import type { DateRange } from 'react-day-picker'
import { i18n } from '@/lib/i18n'
import type { StatusRegistry } from '@/components/common/StatusBadge'

export const ORDER_STATUSES = [
  'pending',
  'approved',
  'paid',
  'shipped',
  'cancelled',
  'refunded',
] as const
export type OrderStatus = (typeof ORDER_STATUSES)[number]

const ORDER_STATUS_TONE: Record<OrderStatus, StatusRegistry<OrderStatus>[OrderStatus]['tone']> = {
  pending: 'amber',
  approved: 'blue',
  paid: 'green',
  shipped: 'teal',
  cancelled: 'gray',
  refunded: 'red',
}

/** Resolved at call time — module-scope callers get whatever language is active on import. */
export function orderStatusMeta(): StatusRegistry<OrderStatus> {
  return ORDER_STATUSES.reduce((meta, status) => {
    meta[status] = {
      label: i18n.t(`orderStatus.${status}`, { ns: 'demo-filters' }),
      tone: ORDER_STATUS_TONE[status],
    }
    return meta
  }, {} as StatusRegistry<OrderStatus>)
}

/** The reactive form — re-renders when the language changes. */
export function useOrderStatusMeta(): StatusRegistry<OrderStatus> {
  const { t } = useTranslation('demo-filters')
  return ORDER_STATUSES.reduce((meta, status) => {
    meta[status] = { label: t(`orderStatus.${status}`), tone: ORDER_STATUS_TONE[status] }
    return meta
  }, {} as StatusRegistry<OrderStatus>)
}

export const CHANNELS = ['direct', 'partner', 'online'] as const
export type Channel = (typeof CHANNELS)[number]

export function channelLabels(): Record<Channel, string> {
  return {
    direct: i18n.t('channel.direct', { ns: 'demo-filters' }),
    partner: i18n.t('channel.partner', { ns: 'demo-filters' }),
    online: i18n.t('channel.online', { ns: 'demo-filters' }),
  }
}

export function useChannelLabels(): Record<Channel, string> {
  const { t } = useTranslation('demo-filters')
  return { direct: t('channel.direct'), partner: t('channel.partner'), online: t('channel.online') }
}

export interface OrderRecord {
  id: string
  account: string
  owner: string
  city: string
  channel: Channel
  status: OrderStatus
  amount: number
  items: number
  /** ISO date, no clock — an order is dated, not timed. */
  createdAt: string
  tags: string[]
}

/**
 * Every filter the panel can express, in one object.
 *
 * One shape rather than a dozen `useState` calls, because three things need
 * to read the whole thing at once: the chip row, the saved views, and the
 * "kaç filtre aktif" count on the toggle.
 */
export interface FilterState {
  search: string
  statuses: OrderStatus[]
  owners: string[]
  channels: Channel[]
  cities: string[]
  amount: { min: number | null; max: number | null }
  dates: DateRange | null
  /** A boolean filter still belongs in the chip row, so it is state like any other. */
  onlyTagged: boolean
}

export const EMPTY_FILTER: FilterState = {
  search: '',
  statuses: [],
  owners: [],
  channels: [],
  cities: [],
  amount: { min: null, max: null },
  dates: null,
  onlyTagged: false,
}

export const SORT_FIELDS = ['createdAt', 'amount', 'account', 'items', 'status'] as const
export type SortField = (typeof SORT_FIELDS)[number]

export function sortLabels(): Record<SortField, string> {
  return SORT_FIELDS.reduce(
    (labels, field) => {
      labels[field] = i18n.t(`sortField.${field}`, { ns: 'demo-filters' })
      return labels
    },
    {} as Record<SortField, string>,
  )
}

export function useSortLabels(): Record<SortField, string> {
  const { t } = useTranslation('demo-filters')
  return SORT_FIELDS.reduce(
    (labels, field) => {
      labels[field] = t(`sortField.${field}`)
      return labels
    },
    {} as Record<SortField, string>,
  )
}

/** Ascending is rarely the first question for money or dates. */
export const SORT_DESC_FIRST: Record<SortField, boolean> = {
  createdAt: true,
  amount: true,
  account: false,
  items: true,
  status: false,
}

export interface SortRule {
  field: SortField
  direction: 'asc' | 'desc'
}

export interface SavedView {
  id: string
  label: string
  description: string
  filter: Partial<FilterState>
  sort: SortRule[]
  /**
   * Team-shared views need a backend to resolve ownership and visibility
   * (#27 in `docs/frontend-platform/ENTERPRISE_LAYERS_ASSESSMENT.md`) — this
   * only reserves the field. Personal views (see `lib/personalViews.ts`)
   * default it to `false`; the curated `SAVED_VIEWS` leave it unset.
   */
  shared?: boolean
}
