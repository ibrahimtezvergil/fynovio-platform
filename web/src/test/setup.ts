import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterAll, afterEach, beforeAll, beforeEach } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { server } from '@/mocks/server'
import { url } from '@/test/authHandlers'

// MSW intercepts network calls in tests the same way it does in the browser —
// component tests exercise the real queryFn/apiClient path, not a stub.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
// A tenant with no custom fields unless a test says otherwise: the opportunity form, detail and list all read the
// definitions, and most tests are about something else. Per test (not in `handlers`), so dev MSW never answers it.
beforeEach(() => server.use(http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json([]))))
afterEach(() => {
  server.resetHandlers()
  cleanup()
})
afterAll(() => server.close())
