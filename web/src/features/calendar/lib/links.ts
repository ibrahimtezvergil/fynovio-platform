import type { TFunction } from 'i18next'
import { paths } from '@/routes/paths'
import type { CalendarLink, CalendarLinkRef } from '../schema'

/** Targets the API accepts in v1 (api-contract.md, "Create / replace body"). */
const SUPPORTED_TARGETS = new Set(['crm/opportunity', 'masterdata/party'])

const isTarget = (ref: CalendarLinkRef, boundedContext: string, entityType: string) =>
  ref.boundedContext === boundedContext && ref.entityType === entityType

/** In-app route of a linked record, or `null` when the target type has no page (Party, in v1). */
export function routeForLink(ref: CalendarLinkRef): string | null {
  return isTarget(ref, 'crm', 'opportunity') ? paths.crmOpportunity(ref.id) : null
}

/**
 * How a link may be presented: navigation only for an `accessible` target that has a route, a plain label for any other
 * accessible target, and nothing at all for an `unavailable` one — its label is never rendered, and there is no route
 * to it, so a caller who lost access cannot even learn that a page exists.
 */
export type LinkPresentation =
  | { kind: 'route'; to: string; label: string; subtitle?: string }
  | { kind: 'label'; label: string; subtitle?: string }
  | { kind: 'none' }

export function presentLink(link: CalendarLink | null, fallbackLabel: (ref: CalendarLinkRef) => string): LinkPresentation {
  if (!link || link.state !== 'accessible') return { kind: 'none' }
  const label = link.label ?? fallbackLabel(link.ref)
  const to = routeForLink(link.ref)
  return to ? { kind: 'route', to, label, subtitle: link.subtitle } : { kind: 'label', label, subtitle: link.subtitle }
}

/** `?link=crm/opportunity/17` → a ref, or `null` for anything malformed or unsupported (the API validates again regardless). */
export function parseLinkParam(value: string | null): CalendarLinkRef | null {
  const match = value?.match(/^([a-z]+)\/([a-z]+)\/(\d+)$/)
  if (!match || !SUPPORTED_TARGETS.has(`${match[1]}/${match[2]}`)) return null
  const id = Number(match[3])
  return Number.isSafeInteger(id) && id > 0 ? { boundedContext: match[1], entityType: match[2], id } : null
}

const TARGET_KEYS: Record<string, string> = { 'crm/opportunity': 'opportunity', 'masterdata/party': 'party' }

/** A name for a target whose label the server did not (or could not yet) supply, e.g. a link just prefilled from a record. */
export function linkFallbackLabel(t: TFunction<'calendar'>, ref: CalendarLinkRef): string {
  const key = TARGET_KEYS[`${ref.boundedContext}/${ref.entityType}`] ?? 'unknown'
  return t(`link.fallback.${key}`, { id: ref.id })
}
