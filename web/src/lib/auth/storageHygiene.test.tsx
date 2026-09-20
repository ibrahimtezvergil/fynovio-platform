import { render, screen } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import { server } from '@/mocks/server'
import {
  authenticated, login as loginHandler, logout as logoutHandler, problem, refresh, selectionRequired, selectTenantHandler,
} from '@/test/authHandlers'
import { resetSession } from '@/test/session'
import { useSessionStore } from './session'
import { SessionGate } from './SessionGate'
import { LEGACY_STORAGE_KEY, login, logout, refreshSession, selectTenant } from './sessionClient'

const SECRETS = ['tok-login', 'tok-refreshed', 'tok-tenant', 'pw-secret-123', 'ada@example.com']

function dump(storage: Storage): string {
  return JSON.stringify(Object.fromEntries(Array.from({ length: storage.length }, (_, i) => [storage.key(i)!, storage.getItem(storage.key(i)!)])))
}

beforeEach(() => {
  localStorage.clear()
  sessionStorage.clear()
  resetSession()
})

describe('browser storage hygiene', () => {
  it('after a full login → tenant select → refresh → logout flow neither storage holds anything auth-related', async () => {
    server.use(
      loginHandler(selectionRequired()),
      selectTenantHandler(() => HttpResponse.json({ accessToken: 'tok-tenant', expiresIn: 600, activeTenant: { tenantId: 2 } })),
      refresh(() => HttpResponse.json(authenticated({ accessToken: 'tok-refreshed' }))),
      logoutHandler(),
    )

    const snapshots: string[] = []
    const snap = () => snapshots.push(dump(localStorage), dump(sessionStorage))

    await login({ email: 'ada@example.com', password: 'pw-secret-123' }); snap()
    await selectTenant(2); snap()
    await refreshSession(); snap()
    expect(useSessionStore.getState().accessToken).toBe('tok-refreshed') // it lives in memory…
    await logout(); snap()

    const all = snapshots.join('')
    for (const secret of SECRETS) expect(all).not.toContain(secret) // …and only there
    expect(all).not.toMatch(/token|refresh|session|auth/i)
  })

  it('the session store is not persisted: it has no persist middleware API', () => {
    expect((useSessionStore as unknown as { persist?: unknown }).persist).toBeUndefined()
  })

  it('SessionGate purges the legacy mock-era `fynovio-auth` entry at boot', async () => {
    localStorage.setItem(LEGACY_STORAGE_KEY, JSON.stringify({ state: { isAuthenticated: true, token: 'mock-token' } }))
    server.use(refresh(() => problem(401, 'session_invalid')))

    render(<I18nextProvider i18n={i18n}><SessionGate><p>APP</p></SessionGate></I18nextProvider>)

    expect(await screen.findByText('APP')).toBeInTheDocument()
    expect(localStorage.getItem(LEGACY_STORAGE_KEY)).toBeNull()
  })

  it('a legacy persisted "authenticated" flag cannot restore a session by itself', async () => {
    localStorage.setItem(LEGACY_STORAGE_KEY, JSON.stringify({ state: { isAuthenticated: true, user: { name: 'Deniz' }, token: 'mock-token' } }))
    server.use(refresh(() => problem(401, 'session_invalid')))

    render(<I18nextProvider i18n={i18n}><SessionGate><p>APP</p></SessionGate></I18nextProvider>)
    await screen.findByText('APP')

    expect(useSessionStore.getState().status).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })
})

describe('SessionGate', () => {
  it('renders no app content while the session is unknown, then the app once resolved', async () => {
    let release!: () => void
    const gate = new Promise<void>((r) => { release = r })
    server.use(refresh(async () => { await gate; return HttpResponse.json(authenticated()) }))

    render(<I18nextProvider i18n={i18n}><SessionGate><p>APP</p></SessionGate></I18nextProvider>)

    expect(screen.queryByText('APP')).not.toBeInTheDocument()
    expect(document.querySelector('[aria-busy="true"]')).toBeInTheDocument()

    release()
    expect(await screen.findByText('APP')).toBeInTheDocument()
  })
})
