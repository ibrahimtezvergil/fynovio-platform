import { expect, test } from '@playwright/test'
import { adminToken, createMember, invite, login, mailFor, newApi, uniqueEmail } from './support/api.ts'
import { NEW_PASSWORD, OTHER_PASSWORD, SEED_PASSWORD } from './support/env.ts'
import { emailField, openUserMenu, passwordField, signIn, signInAndWait, t } from './support/ui.ts'

const newPassword = (page: import('@playwright/test').Page) => passwordField(page, t.auth.passwordFields.newPassword)
const confirmPassword = (page: import('@playwright/test').Page) => passwordField(page, t.auth.passwordFields.confirmPassword)

test.describe('password lifecycle', () => {
  test('forgot → reset link → new password → sign in; the link works once', async ({ page }) => {
    const email = await createMember(1, SEED_PASSWORD)

    await page.goto('/forgot-password')
    await page.getByLabel(t.auth.forgotPassword.emailLabel).fill(email)
    await page.getByRole('button', { name: t.auth.forgotPassword.submit }).click()
    await expect(page.getByText(t.auth.forgotPassword.sentDescription)).toBeVisible()

    const mail = await mailFor(email, 'password_reset')
    expect(mail.link).toContain('/reset-password#token=')

    await page.goto(mail.link)
    await expect(page).toHaveURL(/\/reset-password$/) // the token left the address bar
    await newPassword(page).fill(NEW_PASSWORD)
    await confirmPassword(page).fill(NEW_PASSWORD)
    await page.getByRole('button', { name: t.auth.resetPassword.submit }).click()

    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByText(t.auth.loginPage.passwordUpdated)).toBeVisible()
    await emailField(page).fill(email)
    await passwordField(page).fill(NEW_PASSWORD)
    await page.getByRole('button', { name: t.auth.loginForm.submit }).click()
    await expect(page).toHaveURL(/\/dashboard$/)

    const api = await newApi()
    expect((await login(api, email, SEED_PASSWORD)).response.status()).toBe(401) // the old password is gone
    await api.dispose()

    // The same link a second time: the page cannot know it was used, so the form appears — and the server's
    // refusal turns it into the one generic invalid-link screen.
    await page.context().clearCookies()
    await page.goto(mail.link)
    await newPassword(page).fill(OTHER_PASSWORD)
    await confirmPassword(page).fill(OTHER_PASSWORD)
    await page.getByRole('button', { name: t.auth.resetPassword.submit }).click()
    await expect(page.getByText(t.auth.linkInvalid.title, { exact: true })).toBeVisible()
    expect((await login(await newApi(), email, OTHER_PASSWORD)).response.status()).toBe(401) // and nothing was changed
  })

  test('a link the server never issued, and a link without a token, both end on the invalid-link screen', async ({ page }) => {
    // A token the server never issued: the form appears (the page cannot know), the server refuses it, and the
    // page replaces the form with the one generic "this link does not work" screen.
    await page.goto('/reset-password#token=00000000-0000-0000-0000-000000000000.not-a-real-secret')
    await newPassword(page).fill(NEW_PASSWORD)
    await confirmPassword(page).fill(NEW_PASSWORD)
    await page.getByRole('button', { name: t.auth.resetPassword.submit }).click()
    await expect(page.getByText(t.auth.linkInvalid.title, { exact: true })).toBeVisible()

    await page.goto('/reset-password') // no token at all: never a form
    await expect(page.getByText(t.auth.linkInvalid.title, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: t.auth.resetPassword.submit })).toHaveCount(0)
  })

  test('changing the password keeps this session and signs the other device out cleanly', async ({ browser }) => {
    const email = await createMember(1, SEED_PASSWORD)
    const contextA = await browser.newContext({ locale: 'tr-TR' })
    const contextB = await browser.newContext({ locale: 'tr-TR' })
    const a = await contextA.newPage()
    const b = await contextB.newPage()

    await signIn(a, email, SEED_PASSWORD)
    await expect(a).toHaveURL(/\/dashboard$/)
    await signIn(b, email, SEED_PASSWORD)
    await expect(b).toHaveURL(/\/dashboard$/)
    await b.goto('/crm/opportunities')
    await expect(b).toHaveURL(/\/crm\/opportunities$/)

    await a.goto('/account/security')
    await passwordField(a, t.auth.passwordFields.currentPassword).fill(SEED_PASSWORD)
    await newPassword(a).fill(NEW_PASSWORD)
    await confirmPassword(a).fill(NEW_PASSWORD)
    await a.getByRole('button', { name: t.auth.accountSecurity.submit }).click()
    await expect(a.getByText(t.auth.accountSecurity.changed)).toBeVisible()

    await a.reload() // this session survives
    await expect(a).toHaveURL(/\/account\/security$/)

    await b.reload() // the other device's session was revoked: a clean redirect to login carrying the page it was on
    await expect(b).toHaveURL(/\/login\?returnUrl=%2Fcrm%2Fpipeline$/)

    await emailField(b).fill(email)
    await passwordField(b).fill(NEW_PASSWORD)
    await b.getByRole('button', { name: t.auth.loginForm.submit }).click()
    await expect(b).toHaveURL(/\/crm\/pipeline$/)

    await contextA.close()
    await contextB.close()
  })

  test('a wrong current password is an inline error and does not sign the user out', async ({ page }) => {
    const email = await createMember(1, SEED_PASSWORD)
    await signInAndWait(page, email, SEED_PASSWORD)
    await page.goto('/account/security')

    await passwordField(page, t.auth.passwordFields.currentPassword).fill('not-my-password-at-all')
    await newPassword(page).fill(NEW_PASSWORD)
    await confirmPassword(page).fill(NEW_PASSWORD)
    await page.getByRole('button', { name: t.auth.accountSecurity.submit }).click()

    await expect(page.getByText(t.auth.accountSecurity.currentWrong)).toBeVisible()
    await expect(page).toHaveURL(/\/account\/security$/)
    await page.reload()
    await expect(page).toHaveURL(/\/account\/security$/) // still signed in
  })

  test('the security page is reachable from the user menu', async ({ page }) => {
    await signInAndWait(page, await createMember(1, SEED_PASSWORD), SEED_PASSWORD)
    await openUserMenu(page)
    await page.getByRole('button', { name: t.nav.userMenu.security }).click()
    await expect(page).toHaveURL(/\/account\/security$/)
  })
})

test.describe('invitations', () => {
  test('a new person accepts an invitation: name + password, then straight into the app', async ({ page }) => {
    const token = await adminToken(1)
    const email = uniqueEmail('newcomer')
    await invite(token, 1, email, 'Yeni Kişi')
    const mail = await mailFor(email, 'invite')
    expect(mail.link).toContain('/accept-invite#token=')

    await page.goto(mail.link)
    await expect(page).toHaveURL(/\/accept-invite$/) // the token left the address bar
    await expect(page.getByText(t.auth.acceptInvite.forAddress.replace('{{email}}', `${email[0]}***${email.slice(email.indexOf('@'))}`))).toBeVisible()
    await expect(page.getByLabel(t.auth.acceptInvite.nameLabel)).toHaveValue('Yeni Kişi')
    await newPassword(page).fill(NEW_PASSWORD)
    await confirmPassword(page).fill(NEW_PASSWORD)
    await page.getByRole('button', { name: t.auth.acceptInvite.submit }).click()

    await expect(page).toHaveURL(/\/dashboard$/)
    await page.reload()
    await expect(page).toHaveURL(/\/dashboard$/) // a real session, restored from the cookie

    // Used once: the same link is now dead.
    await page.context().clearCookies()
    await page.goto(mail.link)
    await expect(page.getByText(t.auth.linkInvalid.title, { exact: true })).toBeVisible()
  })

  test('someone who already has an account proves their password and joins a second organisation', async ({ page }) => {
    const email = await createMember(1, SEED_PASSWORD)
    await invite(await adminToken(2), 2, email)
    const mail = await mailFor(email, 'invite')

    await page.goto(mail.link)
    await expect(page.getByLabel(t.auth.acceptInvite.nameLabel)).toHaveCount(0) // no name, no confirmation: they already have an account
    await passwordField(page, t.auth.acceptInvite.existingPasswordLabel).fill(OTHER_PASSWORD)
    await page.getByRole('button', { name: t.auth.acceptInvite.submit }).click()
    await expect(page.getByText(t.auth.acceptInvite.wrongPassword)).toBeVisible() // the invitation is not burnt by a wrong password

    await passwordField(page, t.auth.acceptInvite.existingPasswordLabel).fill(SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.acceptInvite.submit }).click()

    await expect(page).toHaveURL(/\/select-tenant/) // now a member of two organisations
    await expect(page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '2') })).toBeVisible()
  })

  test('an invitation link opens even in a browser that is already signed in', async ({ page }) => {
    await signIn(page, await createMember(1, SEED_PASSWORD), SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/)

    const email = uniqueEmail('second')
    await invite(await adminToken(1), 1, email)
    await page.goto((await mailFor(email, 'invite')).link)

    await expect(page.getByLabel(t.auth.acceptInvite.nameLabel)).toBeVisible()
  })
})
