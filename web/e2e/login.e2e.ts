import { expect, test } from '@playwright/test'
import { ADMIN, APP_URL, NO_MEMBERSHIP, SEED_PASSWORD, SINGLE } from './support/env.ts'
import { emailField, expectNoTokenInStorage, openUserMenu, passwordField, signIn, signOut, t, userMenuTrigger } from './support/ui.ts'

test.describe('signing in and out', () => {
  test('signs in, survives a reload, holds no token in web storage, and signs out for good', async ({ page }) => {
    await signIn(page, SINGLE, SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/)
    await expectNoTokenInStorage(page)

    await page.reload() // the session is rebuilt from the HttpOnly refresh cookie
    await expect(page).toHaveURL(/\/dashboard$/)
    await expect(userMenuTrigger(page)).toBeVisible()

    await signOut(page)

    // The refresh cookie is gone: the server-side session is dead too.
    const refresh = await page.request.post('/api/auth/refresh', { headers: { 'X-Requested-With': 'fynovio' } })
    expect(refresh.status()).toBe(401)

    await page.goto('/crm/pipeline')
    await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcrm%2Fpipeline$/)
  })

  test('wrong credentials: one generic message, still on the login page', async ({ page }) => {
    await signIn(page, SINGLE, 'definitely-not-the-password')

    await expect(page.getByRole('alert')).toHaveText(t.auth.loginForm.invalidCredentials)
    await expect(page).toHaveURL(/\/login$/)
    await expect(emailField(page)).toHaveValue(SINGLE)
    await expect(passwordField(page)).toHaveValue('')

    // An unknown address gets the identical message — nothing tells the two apart.
    await emailField(page).fill('nobody-at-all@example.com')
    await passwordField(page).fill('definitely-not-the-password')
    await page.getByRole('button', { name: t.auth.loginForm.submit }).click()
    await expect(page.getByRole('alert')).toHaveText(t.auth.loginForm.invalidCredentials)
  })

  test('a protected deep link goes to login and, after signing in, back to the same page', async ({ page }) => {
    await page.goto('/crm/pipeline')
    await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcrm%2Fpipeline$/)

    await emailField(page).fill(SINGLE)
    await passwordField(page).fill(SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.loginForm.submit }).click()

    await expect(page).toHaveURL(/\/crm\/pipeline$/)
  })

  for (const hostile of ['https://evil.example/steal', '//evil.example', '/\\evil.example', 'javascript:alert(1)']) {
    test(`an open-redirect attempt (${hostile}) never leaves the app`, async ({ page }) => {
      await page.goto(`/login?returnUrl=${encodeURIComponent(hostile)}`)
      await emailField(page).fill(SINGLE)
      await passwordField(page).fill(SEED_PASSWORD)
      await page.getByRole('button', { name: t.auth.loginForm.submit }).click()

      await expect(page).toHaveURL(`${APP_URL}/dashboard`)
    })
  }

  test('an administrator of two tenants chooses one, and can switch to the other', async ({ page }) => {
    await signIn(page, ADMIN, SEED_PASSWORD)

    await expect(page).toHaveURL(/\/select-tenant/)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)

    const tenantTwo = page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '2') })
    await openUserMenu(page)
    const selected = page.waitForResponse((r) => r.url().endsWith('/api/auth/tenants/select') && r.request().method() === 'POST')
    await tenantTwo.click() // the server checks the membership and mints a token for tenant 2; the menu closes itself
    const response = await selected
    expect(response.status()).toBe(200)
    expect(response.request().postDataJSON()).toEqual({ tenantId: 2 })

    // The shell re-renders after the switch (caches are cleared), so reopen the menu until it stays open.
    await expect(async () => {
      if (!(await tenantTwo.isVisible())) await openUserMenu(page)
      await expect(tenantTwo).toHaveAttribute('aria-current', 'true', { timeout: 1_000 })
    }).toPass({ timeout: 10_000 })
  })

  test('an account without any membership lands on the no-access page (and is still signed in)', async ({ page }) => {
    await signIn(page, NO_MEMBERSHIP, SEED_PASSWORD)

    await expect(page).toHaveURL(/\/no-access$/)
    await expect(page.getByText(t.auth.noAccess.noMembershipDescription)).toBeVisible()
    await page.getByRole('button', { name: t.auth.noAccess.signOut }).click()
    await expect(page).toHaveURL(/\/login/)
  })

  test('registration is disabled: the invite-only screen, and the API route does not exist', async ({ page }) => {
    await page.goto('/register')
    await expect(page.getByText(t.auth.register.inviteOnlyTitle, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: t.auth.register.submit })).toHaveCount(0)

    const response = await page.request.post('/api/auth/register', {
      data: { email: 'x@e2e.example', displayName: 'X', password: 'E2E-Whatever-Passw0rd-1' },
      headers: { 'X-Requested-With': 'fynovio' },
    })
    expect(response.status()).toBe(404)
  })
})
