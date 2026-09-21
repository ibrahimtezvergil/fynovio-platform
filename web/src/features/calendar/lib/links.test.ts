import { describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import { accessibleLink, unavailableLink } from '@/test/calendar'
import { calendarEntrySchema } from '../schema'
import { wireEntry } from '@/test/calendar'
import { linkFallbackLabel, parseLinkParam, presentLink, routeForLink } from './links'

const linkOf = (wire: unknown) => calendarEntrySchema.parse(wireEntry({ link: wire })).link
const t = i18n.getFixedT('tr', 'calendar')
const fallback = (ref: Parameters<typeof linkFallbackLabel>[1]) => linkFallbackLabel(t, ref)

describe('presentLink — navigation only where there is a route and access', () => {
  it('an accessible crm/opportunity navigates to /crm/opportunities/:id', () => {
    expect(presentLink(linkOf(accessibleLink()), fallback)).toEqual({ kind: 'route', to: '/crm/opportunities/17', label: 'OPP-17 — Acme', subtitle: 'Proposal' })
  })

  it('an accessible target without a route (masterdata/party in v1) is a plain label, never a link', () => {
    const party = accessibleLink({ ref: { boundedContext: 'masterdata', entityType: 'party', id: 5 }, label: 'Acme Ltd', subtitle: undefined })
    expect(presentLink(linkOf(party), fallback)).toEqual({ kind: 'label', label: 'Acme Ltd', subtitle: undefined })
  })

  it('an unavailable link presents nothing at all — not even its label', () => {
    expect(presentLink(linkOf({ ...unavailableLink(), label: 'Secret deal' }), fallback)).toEqual({ kind: 'none' })
  })

  it('no link presents nothing', () => {
    expect(presentLink(null, fallback)).toEqual({ kind: 'none' })
  })

  it('an accessible link that arrived without a label falls back to a generic name, not to an empty string', () => {
    expect(presentLink(linkOf(accessibleLink({ label: undefined, subtitle: undefined })), fallback)).toMatchObject({ kind: 'route', label: 'Fırsat #17' })
  })
})

describe('routeForLink', () => {
  it('only crm/opportunity has a route', () => {
    expect(routeForLink({ boundedContext: 'crm', entityType: 'opportunity', id: 9 })).toBe('/crm/opportunities/9')
    expect(routeForLink({ boundedContext: 'masterdata', entityType: 'party', id: 9 })).toBeNull()
    expect(routeForLink({ boundedContext: 'crm', entityType: 'quote', id: 9 })).toBeNull()
  })
})

describe('parseLinkParam — the "Add to calendar" hand-off', () => {
  it('parses a supported target', () => {
    expect(parseLinkParam('crm/opportunity/17')).toEqual({ boundedContext: 'crm', entityType: 'opportunity', id: 17 })
    expect(parseLinkParam('masterdata/party/3')).toEqual({ boundedContext: 'masterdata', entityType: 'party', id: 3 })
  })

  it.each([null, '', 'crm/opportunity', 'crm/opportunity/0', 'crm/opportunity/-1', 'crm/opportunity/1.5', 'crm/opportunity/abc', 'crm/quote/1', 'CRM/opportunity/1', 'crm/opportunity/17/x', '../etc/passwd'])(
    'ignores %j',
    (value) => expect(parseLinkParam(value)).toBeNull(),
  )
})
