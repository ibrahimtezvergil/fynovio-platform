import { expect, type Page } from '@playwright/test'
import auth from '../../src/locales/tr/auth.ts'
import nav from '../../src/locales/tr/nav.ts'
import opportunities from '../../src/locales/tr/opportunities.ts'

/** The UI's own Turkish copy (the default language), read from the same catalogs — no hard-coded strings to drift. */
export const t = { auth, nav, opportunities }

export const emailField = (page: Page) => page.getByLabel(auth.loginForm.emailLabel, { exact: true })
export const passwordField = (page: Page, label: string = auth.loginForm.passwordLabel) => page.getByLabel(label, { exact: true })

export async function signIn(page: Page, email: string, password: string) {
  await page.goto('/login')
  await emailField(page).fill(email)
  await passwordField(page).fill(password)
  await page.getByRole('button', { name: auth.loginForm.submit }).click()
}

/** The avatar button of the shell (other topbar buttons — notifications, messages — are dialog triggers too). */
export const userMenuTrigger = (page: Page) => page.locator('button[aria-haspopup="dialog"]:has(.nx-avatar):visible').first()

export async function openUserMenu(page: Page) {
  await userMenuTrigger(page).click()
}

/** Signs in and waits until the app has actually taken the session (not just until the click). */
export async function signInAndWait(page: Page, email: string, password: string) {
  await signIn(page, email, password)
  await expect(page).not.toHaveURL(/\/login/)
}

export async function signOut(page: Page) {
  await openUserMenu(page)
  await page.getByRole('button', { name: nav.userMenu.logout }).click()
  await expect(page).toHaveURL(/\/login/)
}

/** No credential of any kind may be readable from web storage. */
export async function expectNoTokenInStorage(page: Page) {
  const stored = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }))
  expect(stored).not.toMatch(/eyJ[A-Za-z0-9_-]{10,}/) // a JWT
  expect(stored).not.toMatch(/fynovio_rt/)
}
