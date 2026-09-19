import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { CUSTOMERS } from '@/features/demo-forms/data/options'
import { API_BASE } from '@/mocks/apiBase'

export const demoFormsHandlers = [
  /**
   * A server search, simulated: the same list, filtered on the far side of a
   * delay so the async combobox has something to abort and race against.
   */
  http.get(`${API_BASE}${endpoints.demoForms.customers}`, async ({ request }) => {
    await delay(450)
    const url = new URL(request.url)
    const needle = (url.searchParams.get('q') ?? '').toLocaleLowerCase('tr')
    const results = CUSTOMERS.filter((customer) =>
      `${customer.label} ${customer.description ?? ''}`.toLocaleLowerCase('tr').includes(needle),
    ).slice(0, 8)
    return HttpResponse.json(results)
  }),
]
