import type { DateRange } from 'react-day-picker'
import { i18n } from '@/lib/i18n'
import {
  channelLabels,
  orderStatusMeta,
  type Channel,
  type FilterState,
  type OrderRecord,
  type OrderStatus,
} from '@/features/demo-filters/types'
import { formatDay, formatMoney, type FilterChip } from '@/features/demo-filters/lib/query'

export type FilterNode =
  | { field: 'search'; op: 'contains'; value: string }
  | { field: 'status'; op: 'in'; value: OrderStatus[] }
  | { field: 'owner'; op: 'in'; value: string[] }
  | { field: 'channel'; op: 'in'; value: Channel[] }
  | { field: 'city'; op: 'in'; value: string[] }
  | { field: 'amount'; op: 'range'; value: FilterState['amount'] }
  | { field: 'createdAt'; op: 'range'; value: DateRange }
  | { field: 'onlyTagged'; op: 'bool'; value: true }

/** Converts the current flat controls into explicit predicates; empty controls create no node. */
export function filterStateToNodes(state: FilterState): FilterNode[] {
  const nodes: FilterNode[] = []
  const search = state.search.trim()
  if (search) nodes.push({ field: 'search', op: 'contains', value: search })
  if (state.statuses.length) nodes.push({ field: 'status', op: 'in', value: state.statuses })
  if (state.owners.length) nodes.push({ field: 'owner', op: 'in', value: state.owners })
  if (state.channels.length) nodes.push({ field: 'channel', op: 'in', value: state.channels })
  if (state.cities.length) nodes.push({ field: 'city', op: 'in', value: state.cities })
  if (state.amount.min != null || state.amount.max != null) {
    nodes.push({ field: 'amount', op: 'range', value: state.amount })
  }
  if (state.dates?.from) nodes.push({ field: 'createdAt', op: 'range', value: state.dates })
  if (state.onlyTagged) nodes.push({ field: 'onlyTagged', op: 'bool', value: true })
  return nodes
}

function dayValue(input: Date | string): number {
  const date = typeof input === 'string' ? new Date(input) : input
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()
}

/** Every node is ANDed; OR/grouping is deliberately left to the view-state layer. */
export function applyFilterNodes(nodes: readonly FilterNode[], rows: readonly OrderRecord[]): OrderRecord[] {
  return rows.filter((row) =>
    nodes.every((node) => {
      switch (node.field) {
        case 'search': {
          const haystack = `${row.id} ${row.account} ${row.owner} ${row.city}`.toLocaleLowerCase('tr-TR')
          return haystack.includes(node.value.toLocaleLowerCase('tr-TR'))
        }
        case 'status': return node.value.includes(row.status)
        case 'owner': return node.value.includes(row.owner)
        case 'channel': return node.value.includes(row.channel)
        case 'city': return node.value.includes(row.city)
        case 'amount':
          return (node.value.min == null || row.amount >= node.value.min)
            && (node.value.max == null || row.amount <= node.value.max)
        case 'createdAt':
          return (!node.value.from || dayValue(row.createdAt) >= dayValue(node.value.from))
            && (!node.value.to || dayValue(row.createdAt) <= dayValue(node.value.to))
        case 'onlyTagged': return row.tags.length > 0
      }
    }),
  )
}

/** One removable chip per predicate value, derived from the AST rather than UI control state. */
export function describeFilterNodes(nodes: readonly FilterNode[]): FilterChip[] {
  const t = (key: string, options?: Record<string, unknown>) => i18n.t(key, { ns: 'demo-filters', ...options })
  const statuses = orderStatusMeta()
  const channels = channelLabels()

  return nodes.flatMap((node): FilterChip[] => {
    switch (node.field) {
      case 'search': return [{ key: 'search', label: t('chips.search', { value: node.value }), clear: { search: '' } }]
      case 'status': return node.value.map((status) => ({ key: `status-${status}`, label: t('chips.status', { label: statuses[status].label }), clear: { statuses: node.value.filter((value) => value !== status) } }))
      case 'owner': return node.value.map((owner) => ({ key: `owner-${owner}`, label: t('chips.owner', { owner }), clear: { owners: node.value.filter((value) => value !== owner) } }))
      case 'channel': return node.value.map((channel) => ({ key: `channel-${channel}`, label: t('chips.channel', { label: channels[channel] }), clear: { channels: node.value.filter((value) => value !== channel) } }))
      case 'city': return node.value.map((city) => ({ key: `city-${city}`, label: t('chips.city', { city }), clear: { cities: node.value.filter((value) => value !== city) } }))
      case 'amount': {
        const min = node.value.min != null ? formatMoney(node.value.min) : '—'
        const max = node.value.max != null ? formatMoney(node.value.max) : '—'
        return [{ key: 'amount', label: t('chips.amount', { min, max }), clear: { amount: { min: null, max: null } } }]
      }
      case 'createdAt': {
        const from = formatDay(node.value.from!.toISOString())
        const to = node.value.to ? formatDay(node.value.to.toISOString()) : '…'
        return [{ key: 'dates', label: t('chips.dates', { from, to }), clear: { dates: null } }]
      }
      case 'onlyTagged': return [{ key: 'tagged', label: t('chips.onlyTagged'), clear: { onlyTagged: false } }]
    }
  })
}
