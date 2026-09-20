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

export interface Recorded {
  method: string
  path: string
  body: unknown
  idempotencyKey: string | null
}

/** Records every Opportunity request so a test can assert on what actually went over the wire. */
export function recordRequests() {
  const seen: Recorded[] = []
  const record = async (request: Request): Promise<void> => {
    let body: unknown = null
    if (request.method !== 'GET') body = await request.clone().json().catch(() => null)
    seen.push({ method: request.method, path: new URL(request.url).pathname, body, idempotencyKey: request.headers.get('Idempotency-Key') })
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
