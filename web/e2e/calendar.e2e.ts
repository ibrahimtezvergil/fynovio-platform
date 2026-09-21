import { expect, test } from '@playwright/test'
import { adminToken } from './support/api.ts'
import { calendar, createEntry, listEntries } from './support/calendar.ts'
import { ADMIN, SEED_PASSWORD, SINGLE } from './support/env.ts'
import { createViaApi, salesRepToken, tokenFor } from './support/opportunities.ts'
import { signIn, t, tenantLabel } from './support/ui.ts'

/**
 * The calendar against the REAL API (no MSW). Needs the Collaboration module migrated and its runtime-role grants in
 * the throwaway database (scripts/e2e.sh). Drag/resize is covered by the vitest hook tests; the browser check of it
 * belongs to the closure stage, where the real grid is driven by hand.
 */
async function signInToCalendar(page: import('@playwright/test').Page) {
  await signIn(page, ADMIN, SEED_PASSWORD)
  await page.getByRole('button', { name: tenantLabel(1) }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
  await page.goto('/calendar')
  await expect(page.getByRole('heading', { name: calendar.page.title, level: 1 })).toBeVisible()
}

test.describe('Calendar — real API', () => {
  test('1: create, edit and delete an entry through the dialog', async ({ page }) => {
    const title = `E2E dialog ${Date.now().toString(36)}`
    await signInToCalendar(page)

    // Create
    await page.getByRole('button', { name: calendar.createEvent }).click()
    const dialog = page.getByRole('dialog', { name: calendar.form.createTitle })
    await dialog.getByLabel(calendar.form.title.label, { exact: true }).fill(title)
    await dialog.getByRole('button', { name: '#28B478' }).click()
    await dialog.getByRole('button', { name: calendar.form.save }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByText(title).first()).toBeVisible()

    const token = await adminToken(1)
    const created = (await listEntries(token)).find((entry) => entry.title === title)
    expect(created?.color).toBe('#28b478') // stored lower-case
    expect(created?.startAt).toMatch(/\+00:00$/) // the server hands instants back in UTC

    // Edit
    await page.getByText(title).first().click()
    const detail = page.getByRole('dialog', { name: title })
    await detail.getByRole('button', { name: calendar.edit }).click()
    await detail.getByLabel(calendar.form.title.label, { exact: true }).fill(`${title} (edited)`)
    await detail.getByRole('button', { name: calendar.form.save }).click()
    await expect(page.getByText(`${title} (edited)`).first()).toBeVisible()
    expect((await listEntries(token)).find((entry) => entry.id === created?.id)?.rowVersion).toBeGreaterThan(created?.rowVersion ?? 0)

    // Delete
    await page.getByText(`${title} (edited)`).first().click()
    const edited = page.getByRole('dialog', { name: `${title} (edited)` })
    await edited.getByRole('button', { name: calendar.delete.action }).click()
    await edited.getByRole('button', { name: calendar.delete.confirm }).click()
    await expect(page.getByText(`${title} (edited)`)).toHaveCount(0)
    expect((await listEntries(token)).some((entry) => entry.id === created?.id)).toBe(false)
  })

  test('2: an opportunity link is a real link to the record, and "Add to calendar" prefills it', async ({ page }) => {
    const token = await adminToken(1)
    const opportunityId = await createViaApi(token, { currency: 'EUR', estimatedAmount: 100 })
    const title = `E2E linked ${Date.now().toString(36)}`

    await signInToCalendar(page)
    await page.goto(`/crm/opportunities/${opportunityId}`)
    await page.getByRole('link', { name: t.opportunities.detail.addToCalendar }).click()

    await expect(page).toHaveURL(/\/calendar$/) // the parameter is consumed
    const dialog = page.getByRole('dialog', { name: calendar.form.createTitle })
    await expect(dialog.getByText(calendar.link.fallback.opportunity.replace('{{id}}', String(opportunityId)))).toBeVisible()
    await dialog.getByLabel(calendar.form.title.label, { exact: true }).fill(title)
    await dialog.getByRole('button', { name: calendar.form.save }).click()
    await expect(dialog).toBeHidden()

    const entry = (await listEntries(token)).find((candidate) => candidate.title === title)
    expect(entry?.link).toMatchObject({ state: 'accessible', ref: { boundedContext: 'crm', entityType: 'opportunity', id: opportunityId } })

    await page.getByText(title).first().click()
    const detail = page.getByRole('dialog', { name: title })
    await detail.getByRole('link', { name: new RegExp(calendar.link.open) }).click()
    await expect(page).toHaveURL(new RegExp(`/crm/opportunities/${opportunityId}$`))
  })

  test('3: entries are personal and tenant-scoped', async () => {
    const admin1 = await adminToken(1)
    const admin2 = await adminToken(2)
    const rep = await salesRepToken()
    const title = `E2E private ${Date.now().toString(36)}`
    expect((await createEntry(admin1, { title })).status).toBe(201)

    expect((await listEntries(admin1)).some((entry) => entry.title === title)).toBe(true)
    expect((await listEntries(rep)).some((entry) => entry.title === title)).toBe(false) // another user, same tenant
    expect((await listEntries(admin2)).some((entry) => entry.title === title)).toBe(false) // same user, other tenant
  })

  test('4: a link the caller cannot use is refused identically to one that does not exist (no oracle)', async () => {
    const admin = await adminToken(1)
    const opportunityId = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 100 })
    const noGrants = await tokenFor(SINGLE)

    const unauthorized = await createEntry(noGrants, { link: { boundedContext: 'crm', entityType: 'opportunity', id: opportunityId } })
    const missing = await createEntry(noGrants, { link: { boundedContext: 'crm', entityType: 'opportunity', id: 999_999_999 } })
    expect(unauthorized.status).toBe(422)
    expect(missing.status).toBe(422)
    expect(unauthorized.body?.type).toBe('link_target_unavailable')
    expect(missing.body?.type).toBe('link_target_unavailable')
  })
})
