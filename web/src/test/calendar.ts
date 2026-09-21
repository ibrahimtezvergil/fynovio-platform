/** Wire shapes (what the Calendar API sends, api-contract.md). Builders only: each test defines its own MSW handlers. */
export const wireEntry = (overrides: Record<string, unknown> = {}) => ({
  id: 42,
  rowVersion: 3,
  title: 'Call with vendor',
  notes: 'Bring the price list',
  color: '#3b82f6',
  allDay: false,
  startAt: '2026-09-21T06:00:00+00:00',
  endAt: '2026-09-21T07:00:00+00:00',
  startDate: null,
  endDate: null,
  link: null,
  ...overrides,
})

export const wireAllDayEntry = (overrides: Record<string, unknown> = {}) =>
  wireEntry({ allDay: true, startAt: null, endAt: null, startDate: '2026-09-21', endDate: '2026-09-22', ...overrides })

export const accessibleLink = (overrides: Record<string, unknown> = {}) => ({
  ref: { boundedContext: 'crm', entityType: 'opportunity', id: 17 },
  state: 'accessible',
  label: 'OPP-17 — Acme',
  subtitle: 'Proposal',
  ...overrides,
})

export const unavailableLink = (ref = { boundedContext: 'crm', entityType: 'opportunity', id: 17 }) => ({ ref, state: 'unavailable' })
