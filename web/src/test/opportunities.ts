import { fireEvent, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { server } from '@/mocks/server'
import { url } from '@/test/authHandlers'
export { PAGE_SIZE } from '@/features/opportunities/api'

/** Wire shapes (what the .NET API really sends — note the integer lifecycle status). */
export const wireOpportunity = (overrides: Record<string, unknown> = {}) => ({
  id: 12,
  status: 1,
  partyId: 1001,
  assignedPrincipalIssuer: 'https://platform.example',
  assignedPrincipalSubject: 'account-7',
  currency: 'EUR',
  estimatedAmount: 250,
  totalAmount: null,
  pipelineDefinitionVersionId: 3,
  pipelineStageId: 30,
  lostReason: null,
  expiryDate: '2030-01-01T23:59:59Z',
  openedDate: '2029-12-01T10:00:00Z',
  wonDate: null,
  lostDate: null,
  rowVersion: 5,
  lines: [{ id: 1, quantity: 2, unitPrice: 50, lineTotal: 100, isOptional: false, isCanceled: false }],
  ...overrides,
})

export const noActions = { canOpen: false, canChangeStage: false, allowedTargetStageIds: [], canWin: false, canLose: false, canReassign: false }

export const stages = [
  { id: 30, name: 'Qualification', sortOrder: 10, isActive: true, isEntry: true },
  { id: 31, name: 'Proposal', sortOrder: 20, isActive: true, isEntry: false },
  { id: 32, name: 'Negotiation', sortOrder: 30, isActive: true, isEntry: false },
  { id: 33, name: 'Legacy stage', sortOrder: 40, isActive: false, isEntry: false },
]

/** The pathname a request for this endpoint carries on the wire (the API base included), as `Recorded.path` reports it. */
export const wirePath = (endpoint: string) => new URL(url(endpoint), 'http://localhost').pathname

export interface Recorded {
  method: string
  path: string
  search: string
  body: unknown
  idempotencyKey: string | null
}

/** Records every Opportunity request so a test can assert on what actually went over the wire. */
export function recordRequests() {
  const seen: Recorded[] = []
  const record = async (request: Request): Promise<void> => {
    let body: unknown = null
    if (request.method !== 'GET') body = await request.clone().json().catch(() => null)
    seen.push({ method: request.method, path: new URL(request.url).pathname, search: new URL(request.url).search, body, idempotencyKey: request.headers.get('Idempotency-Key') })
  }
  return { seen, record, commands: () => seen.filter((entry) => entry.method === 'POST') }
}

export interface DetailMocks {
  detail?: () => Response | Promise<Response>
  actions?: () => Response | Promise<Response>
  stages?: () => Response | Promise<Response>
}

export function mockDetailApi(id: number, recorder: ReturnType<typeof recordRequests>, mocks: DetailMocks = {}) {
  server.use(
    http.get(url(endpoints.opportunities.detail(id)), async ({ request }) => {
      await recorder.record(request)
      return mocks.detail ? mocks.detail() : HttpResponse.json(wireOpportunity({ id }))
    }),
    http.get(url(endpoints.opportunities.actions(id)), async ({ request }) => {
      await recorder.record(request)
      return mocks.actions ? mocks.actions() : HttpResponse.json(noActions)
    }),
    http.get(url(endpoints.pipelines.stages(3)), async ({ request }) => {
      await recorder.record(request)
      return mocks.stages ? mocks.stages() : HttpResponse.json(stages)
    }),
  )
}

export const problemResponse = (status: number, type: string, title = type) => HttpResponse.json({ status, type, title }, { status })

/** Party reference rows as the API sends them (`GET /crm/references/parties`). */
export const wireParty = (id: number, displayName: string, overrides: Record<string, unknown> = {}) => ({
  id,
  partyType: 'Organization',
  displayName,
  email: null,
  ...overrides,
})

export const PARTIES = [wireParty(1001, 'Acme Ltd', { email: 'ops@acme.example' }), wireParty(1002, 'Bora Tekstil'), wireParty(1003, 'Acar Gıda')]

/** The SERVER side of party search: `search` filters by name, `ids` resolves display names. Records every request. */
export function mockParties(rows = PARTIES, recorder?: ReturnType<typeof recordRequests>) {
  server.use(
    http.get(url(endpoints.references.parties), async ({ request }) => {
      await recorder?.record(request)
      const params = new URL(request.url).searchParams
      const ids = params.get('ids')
      if (ids) return HttpResponse.json(rows.filter((row) => ids.split(',').map(Number).includes(row.id)))
      const search = (params.get('search') ?? '').toLowerCase()
      return HttpResponse.json(rows.filter((row) => row.displayName.toLowerCase().includes(search)))
    }),
  )
}

/** Types into a combobox and picks the option the server returned — the only way a test may "choose" anyone. */
export async function chooseFromCombobox(name: string | RegExp, typed: string, optionName: string | RegExp) {
  await typeInCombobox(name, typed)
  fireEvent.click(await screen.findByRole('option', { name: optionName }))
}

/** `inputType` matters: without it Base UI treats the change as browser autofill and never opens the list. */
export async function typeInCombobox(name: string | RegExp, typed: string) {
  const input = screen.getByRole('combobox', { name })
  fireEvent.focus(input)
  fireEvent.input(input, { target: { value: typed }, inputType: 'insertText' })
}

export const wirePrincipal = (subject: string, displayName: string, overrides: Record<string, unknown> = {}) => ({
  issuer: 'https://platform.example',
  subject,
  displayName,
  email: `${subject}@example.test`,
  ...overrides,
})

/** The SERVER side of the Assignable Principals query. The rows are returned verbatim — no client-side filter exists to test. */
export function mockAssignable(id: number, respond: (search: string) => Response | Promise<Response>, recorder?: ReturnType<typeof recordRequests>) {
  server.use(
    http.get(url(endpoints.opportunities.assignablePrincipals(id)), async ({ request }) => {
      await recorder?.record(request)
      return respond(new URL(request.url).searchParams.get('search') ?? '')
    }),
  )
}
