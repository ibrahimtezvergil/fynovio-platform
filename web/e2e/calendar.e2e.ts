import { expect, test } from '@playwright/test'
import { adminToken } from './support/api.ts'
import { calendar, createEntry, listEntries } from './support/calendar.ts'
import { ADMIN, SEED_PASSWORD } from './support/env.ts'
import { createViaApi, salesRepToken } from './support/opportunities.ts'
import { signIn, t, tenantLabel } from './support/ui.ts'

/**
 * The calendar against the REAL API (no MSW). Needs the Collaboration module migrated and its runtime-role grants in
 * the throwaway database (scripts/e2e.sh). Covers the dialog lifecycle, links, tenant/owner scoping, the no-oracle link
 * refusal, a real-grid drag and keyboard/focus behaviour.
 *
 * Not covered here: an entry whose link target became unavailable after it was saved (no API revokes a grant, and no
 * seeded member holds the collaboration role without CRM read). That rule is pinned by Host.Tests (real PDP) and by the
 * vitest dialog/drag tests.
 */
async function signInToCalendar(page: import('@playwright/test').Page) {
  await signIn(page, ADMIN, SEED_PASSWORD)
  await page.getByRole('button', { name: tenantLabel(1) }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
  await page.goto('/calendar')
  await expect(page.getByRole('heading', { name: calendar.page.title, level: 1 })).toBeVisible()
}

/** Today 12:00 local: inside the week grid's visible hours whatever time the suite runs, and never crowded out by month-cell overflow. */
const todayAtNoon = () => {
  const noon = new Date()
  noon.setHours(12, 0, 0, 0)
  return noon
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
    const editing = page.getByRole('dialog', { name: calendar.form.editTitle })
    await editing.getByLabel(calendar.form.title.label, { exact: true }).fill(`${title} (edited)`)
    await editing.getByRole('button', { name: calendar.form.save }).click()
    await expect(page.getByText(`${title} (edited)`).first()).toBeVisible()
    expect((await listEntries(token)).find((entry) => entry.id === created?.id)?.rowVersion).toBeGreaterThan(created?.rowVersion ?? 0)

    // Delete
    await page.getByText(`${title} (edited)`).first().click()
    await page.getByRole('dialog', { name: `${title} (edited)` }).getByRole('button', { name: calendar.delete.action }).click()
    await page.getByRole('dialog', { name: calendar.delete.title }).getByRole('button', { name: calendar.delete.confirm }).click()
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

  test('4: a link to another tenant\'s record is refused identically to one that does not exist (no oracle)', async () => {
    const admin1 = await adminToken(1)
    const opportunityId = await createViaApi(admin1, { currency: 'EUR', estimatedAmount: 100 })
    const admin2 = await adminToken(2) // same person, other tenant: holds the collaboration role there but the record is not theirs to see

    const foreign = await createEntry(admin2, { link: { boundedContext: 'crm', entityType: 'opportunity', id: opportunityId } })
    const missing = await createEntry(admin2, { link: { boundedContext: 'crm', entityType: 'opportunity', id: 999_999_999 } })
    const unknownType = await createEntry(admin2, { link: { boundedContext: 'crm', entityType: 'nonsense', id: opportunityId } })
    for (const refused of [foreign, missing, unknownType]) {
      expect(refused.status).toBe(422)
      expect(refused.body?.type).toBe('link_target_unavailable')
    }
    expect(foreign.body).toEqual(missing.body) // byte-for-byte the same answer
    // The grant-based denial (a member holding the collaboration role but no CRM read) is covered by Host.Tests with the real PDP;
    // no seeded member has that combination.
  })

  test('5: an all-day entry is created through the dialog with an exclusive end date', async ({ page }) => {
    const title = `E2E all-day ${Date.now().toString(36)}`
    await signInToCalendar(page)

    await page.getByRole('button', { name: calendar.createEvent }).click()
    const dialog = page.getByRole('dialog', { name: calendar.form.createTitle })
    await dialog.getByLabel(calendar.form.title.label, { exact: true }).fill(title)
    await dialog.getByRole('switch', { name: calendar.form.allDay }).click()
    await dialog.getByRole('button', { name: calendar.form.save }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByText(title).first()).toBeVisible()

    const created = (await listEntries(await adminToken(1))).find((entry) => entry.title === title)
    expect(created?.allDay).toBe(true)
    expect(created?.startAt).toBeNull()
    expect(created?.startDate).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    const start = new Date(`${created?.startDate}T00:00:00Z`).getTime()
    expect(new Date(`${created?.endDate}T00:00:00Z`).getTime() - start).toBe(86_400_000) // one day = [d, d+1)
  })

  test('6: dragging an entry to the next day in the real grid saves it (PUT with expectedVersion)', async ({ page }) => {
    const admin = await adminToken(1)
    const title = `E2E drag ${Date.now().toString(36)}`
    const start = todayAtNoon()
    expect((await createEntry(admin, { title, startAt: start.toISOString(), endAt: new Date(start.getTime() + 3_600_000).toISOString() })).status).toBe(201)
    const before = (await listEntries(admin)).find((entry) => entry.title === title)!

    await signInToCalendar(page)
    await page.getByRole('tab', { name: calendar.view.week }).click()
    const event = page.getByRole('button', { name: new RegExp(title) })
    await expect(event).toBeVisible()
    const source = (await event.boundingBox())!

    // The week starts on Monday: on a Sunday the only visible neighbour is the day before.
    const direction = start.getDay() === 0 ? -1 : 1
    const startX = source.x + source.width / 2
    const y = source.y + source.height / 2
    await page.mouse.move(startX, y)
    await page.mouse.down()
    await page.mouse.move(startX + 6 * direction, y, { steps: 3 })
    await page.mouse.move(startX + (source.width + 4) * direction, y, { steps: 12 })
    await page.mouse.up()

    await expect
      .poll(async () => (await listEntries(admin)).find((entry) => entry.id === before.id)?.rowVersion, { timeout: 15_000 })
      .toBeGreaterThan(before.rowVersion)
    const after = (await listEntries(admin)).find((entry) => entry.id === before.id)!
    const expected = new Date(before.startAt!)
    expected.setDate(expected.getDate() + direction) // the grid moves by local calendar days
    expect(new Date(after.startAt!).getTime()).toBe(expected.getTime())
    expect(after.title).toBe(title) // a full replace keeps every other field
    expect(after.color).toBe(before.color)
  })

  test('7: an entry is reachable and openable by keyboard, and the dialog traps focus and returns it', async ({ page }) => {
    const admin = await adminToken(1)
    const title = `E2E keyboard ${Date.now().toString(36)}`
    const start = todayAtNoon()
    expect((await createEntry(admin, { title, startAt: start.toISOString(), endAt: new Date(start.getTime() + 3_600_000).toISOString() })).status).toBe(201)

    await signInToCalendar(page)
    await page.getByRole('tab', { name: calendar.view.week }).click()
    const event = page.getByRole('button', { name: new RegExp(title) })
    await expect(event).toBeVisible()
    await event.focus()
    await expect(event).toBeFocused()
    await page.keyboard.press('Enter')

    const dialog = page.getByRole('dialog', { name: title })
    await expect(dialog).toBeVisible()
    const focusInside = () => dialog.evaluate((node) => node.contains(node.ownerDocument.activeElement))
    expect(await focusInside()).toBe(true)
    // The focus manager parks focus on an invisible guard for a beat at each end of the trap, then hands it back inside:
    // what must never happen is focus RESTING on the page behind the dialog.
    for (const key of ['Tab', 'Shift+Tab']) {
      for (let i = 0; i < 12; i += 1) {
        await page.keyboard.press(key)
        await expect.poll(focusInside, { timeout: 2_000 }).toBe(true)
      }
    }

    await page.keyboard.press('Escape')
    await expect(dialog).toBeHidden()
    await expect(event).toBeFocused() // focus goes back to where it came from
  })
})
