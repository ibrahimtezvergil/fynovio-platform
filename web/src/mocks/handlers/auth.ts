import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { API_BASE } from '@/mocks/apiBase'
import type { User } from '@/types'

/** Stand-in until a real `/auth/login` exists. */
const MOCK_USER: User = {
  id: 'usr_1',
  name: 'Deniz Kaya',
  email: 'deniz@fynovio.com',
  role: 'admin',
  initials: 'DK',
}

const MOCK_TOKEN = 'mock-token'

export const authHandlers = [
  http.post(`${API_BASE}${endpoints.auth.login}`, async ({ request }) => {
    await delay(400)
    const { email } = (await request.json()) as { email?: string }
    return HttpResponse.json({ user: { ...MOCK_USER, email: email ?? MOCK_USER.email }, token: MOCK_TOKEN })
  }),
]
