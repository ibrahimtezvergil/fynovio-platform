import { fireEvent, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { url } from '@/test/authHandlers'

export const typeInto = (field: HTMLElement, value: string) => fireEvent.change(field, { target: { value } })

/** Password inputs share their label text with the show/hide toggle, so pick the input itself. */
export const inputByLabel = (label: string) => screen.getByLabelText(label, { selector: 'input' })

/** `GET /auth/config` as the server answers it. */
export const authConfig = (overrides: { selfRegistrationEnabled?: boolean; minLength?: number; maxLength?: number } = {}) =>
  http.get(url(endpoints.auth.config), () =>
    HttpResponse.json({
      selfRegistrationEnabled: overrides.selfRegistrationEnabled ?? false,
      passwordPolicy: { minLength: overrides.minLength ?? 12, maxLength: overrides.maxLength ?? 128 },
    }),
  )

/** A promise the test resolves by hand — holds a request "in flight". */
export function gate() {
  let release!: () => void
  const promise = new Promise<void>((resolve) => {
    release = resolve
  })
  return { promise, release }
}
