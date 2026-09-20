import { expect, test } from '@playwright/test'
import { adminToken, newApi } from './support/api.ts'
import { ADMIN, SALES_REP, SEED_PASSWORD, VIEWER, SINGLE } from './support/env.ts'
import { choosePartyByName, expectNoTokenInStorage, openUserMenu, signIn, t } from './support/ui.ts'
import {
  addLineViaApi,
  assignableViaApi,
  createViaApi,
  getViaApi,
  listViaApi,
  openViaApi,
  reassignViaApi,
  salesRepToken,
  searchPartiesViaApi,
  seededPartyId,
  subjectOf,
  viewerToken,
} from './support/opportunities.ts'

const CSRF = { 'X-Requested-With': 'fynovio' }

test.describe('CRM Opportunities workflow', () => {
  test('1: Login and navigate to the CRM Opportunities list page', async ({ page }) => {
    await signIn(page, ADMIN, SEED_PASSWORD)

    // Select tenant 1
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await expect(page).toHaveURL(/\/dashboard$/)

    // The CRM rail (with its Opportunities entry) only exists inside the CRM application surface.
    await page.goto('/crm/opportunities')
    await expect(page).toHaveURL(/\/crm\/opportunities$/)
    await expect(page.getByRole('link', { name: t.nav.items.opportunities, exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: t.opportunities.list.title })).toBeVisible()

    // Assert the api-mode chip is visible and shows real API
    const apiChip = page.locator('[data-testid="api-mode"]')
    await expect(apiChip).toBeVisible()
    await expect(apiChip).toContainText(t.opportunities.apiMode.real.replace('{{base}}', '/api'))

    await expectNoTokenInStorage(page)
  })

  test('2: Full flow — create, add line, open, move stage, win', async ({ page }) => {
    const admin = await adminToken(1)

    // Sign in and navigate to create form
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/opportunities')
    await page.getByRole('button', { name: t.opportunities.list.newAction, exact: true }).first().click()
    await expect(page).toHaveURL(/\/crm\/opportunities\/new$/)

    // Create opportunity: the customer is chosen from the server-side party search, EUR, 250
    await choosePartyByName(page, 'Acme', 'Acme Corporation')
    await page.getByLabel(t.opportunities.form.currency.label).fill('EUR')
    await page.getByLabel(t.opportunities.form.estimatedAmount.label).fill('250')
    await page.getByRole('button', { name: t.opportunities.form.submit, exact: true }).click()

    // Lands on detail page in Draft status
    await expect(page).toHaveURL(/\/crm\/opportunities\/\d+$/)
    const opportunityId = Number(page.url().split('/').pop())
    await expect(page.getByText(t.opportunities.status.Draft)).toBeVisible()
    // The summary names the customer (through the reference lookup), not just its id.
    await expect(page.getByTestId('summary-party')).toContainText('Acme Corporation')

    // Add a required line: product 77, qty 2, unit price 50
    await page.getByRole('button', { name: t.opportunities.lines.add.action, exact: true }).click()
    const dialog = page.locator('role=dialog')
    await expect(dialog).toBeVisible()
    await page.getByLabel(t.opportunities.lines.add.productId.label).fill('77')
    await page.getByLabel(t.opportunities.lines.add.quantity.label).fill('2')
    await page.getByLabel(t.opportunities.lines.add.unitPrice.label).fill('50')
    // isOptional is false by default
    await page.getByRole('dialog').getByRole('button', { name: t.opportunities.lines.add.submit, exact: true }).click()
    await expect(dialog).not.toBeVisible()

    // Open the opportunity with default expiry
    await page.getByRole('button', { name: t.opportunities.open.action, exact: true }).click()
    await expect(page.locator('role=dialog')).toBeVisible()
    await page.getByRole('dialog').getByRole('button', { name: t.opportunities.open.submit, exact: true }).click()
    await expect(page.locator('role=dialog')).not.toBeVisible()

    // The entry stage assigned by Open is the tenant's configured one.
    const currentStage = page.getByTestId('current-stage')
    await expect(currentStage).toHaveText('Qualification')

    // The selectable targets are exactly the backend-allowed ones, by name: not the current stage, not the retired one.
    const stageSelect = page.getByLabel(t.opportunities.pipeline.move.label)
    await expect(stageSelect.locator('option')).toHaveText([t.opportunities.pipeline.move.placeholder, 'Proposal', 'Negotiation'])
    await stageSelect.selectOption({ label: 'Proposal' })
    await page.getByRole('button', { name: t.opportunities.pipeline.move.submit, exact: true }).click()
    await expect(currentStage).toHaveText('Proposal')

    // Mark as won
    await page.getByRole('button', { name: t.opportunities.win.action, exact: true }).click()
    await expect(page.locator('role=dialog')).toBeVisible()
    await page.getByRole('dialog').getByRole('button', { name: t.opportunities.win.submit, exact: true }).click()
    await expect(page.locator('role=dialog')).not.toBeVisible()

    // Assert terminal state is visible
    await expect(page.locator('[data-testid="terminal-state"]')).toBeVisible()

    // Assert summary total shows 100 EUR (2 x 50)
    const summaryTotal = page.locator('[data-testid="summary-total"]')
    await expect(summaryTotal).toBeVisible()
    // Verify with currency formatting (locale: tr-TR)
    const expectedTotal = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'EUR' }).format(100)
    await expect(summaryTotal).toContainText(expectedTotal)

    // Assert no open/win/lose buttons and no stage select
    await expect(page.getByRole('button', { name: t.opportunities.open.action, exact: true })).not.toBeVisible()
    await expect(page.getByRole('button', { name: t.opportunities.win.action, exact: true })).not.toBeVisible()
    await expect(page.getByRole('button', { name: t.opportunities.lose.action, exact: true })).not.toBeVisible()
    await expect(stageSelect).not.toBeVisible()

    // Verify via API that status is 2 (Won)
    const opp = await getViaApi(admin, opportunityId)
    expect(opp.status).toBe(2)

    await expectNoTokenInStorage(page)
  })

  test('3: Lose flow with reason', async ({ page }) => {
    const admin = await adminToken(1)

    // Prepare via API: create, add line, open
    const id = await createViaApi(admin, { currency: 'USD', estimatedAmount: 500 })
    await addLineViaApi(admin, id, { expectedVersion: 0, productId: 88, quantity: 1, unitPrice: 300, isOptional: false })
    const opp = await getViaApi(admin, id)
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 1)
    const expiryIso = futureDate.toISOString()
    await openViaApi(admin, id, { expectedVersion: opp.rowVersion, expiryDate: expiryIso })

    // Navigate to detail page
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto(`/crm/opportunities/${id}`)

    // Mark as lost with reason
    await page.getByRole('button', { name: t.opportunities.lose.action, exact: true }).click()
    await page.getByLabel(t.opportunities.lose.reason.label).fill('Competitor offer better terms')
    await page.getByRole('dialog').getByRole('button', { name: t.opportunities.lose.submit, exact: true }).click()

    // Assert terminal state and lost reason
    await expect(page.locator('[data-testid="terminal-state"]')).toBeVisible()
    const lostReason = page.locator('[data-testid="summary-lost-reason"]')
    await expect(lostReason).toContainText('Competitor offer better terms')
  })

  test('3b: Lose with blank reason shows validation and creates no request', async ({ page }) => {
    const admin = await adminToken(1)

    const id = await createViaApi(admin, { currency: 'GBP', estimatedAmount: 300 })
    await addLineViaApi(admin, id, { expectedVersion: 0, productId: 89, quantity: 1, unitPrice: 200, isOptional: false })
    const opp = await getViaApi(admin, id)
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 1)
    await openViaApi(admin, id, { expectedVersion: opp.rowVersion, expiryDate: futureDate.toISOString() })

    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto(`/crm/opportunities/${id}`)

    // Track POST requests to /lose
    let loseRequestCount = 0
    page.on('response', (response) => {
      if (response.url().includes('/api/opportunities') && response.url().includes('/lose') && response.request().method() === 'POST') {
        loseRequestCount++
      }
    })

    // Try to lose with blank reason
    await page.getByRole('button', { name: t.opportunities.lose.action, exact: true }).click()
    // Leave reason blank and try to submit
    await page.getByRole('dialog').getByRole('button', { name: t.opportunities.lose.submit, exact: true }).click()

    // Validation error should show
    await expect(page.getByText(t.opportunities.reason.required)).toBeVisible()

    // No request was sent
    expect(loseRequestCount).toBe(0)
  })

  test('4: Validation on create form', async ({ page }) => {
    const admin = await adminToken(1)
    const beforeList = await listViaApi(admin)

    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/opportunities')
    await page.getByRole('button', { name: t.opportunities.list.newAction, exact: true }).first().click()

    // Empty submission
    await page.getByRole('button', { name: t.opportunities.form.submit, exact: true }).click()
    await expect(page).toHaveURL(/\/crm\/opportunities\/new$/) // stays on form
    await expect(page.getByText(t.opportunities.form.partyId.invalid)).toBeVisible()

    // Invalid currency
    await choosePartyByName(page, 'Globex', 'Globex Ltd')
    await page.getByLabel(t.opportunities.form.currency.label).fill('TR') // Too short, should be 3 chars
    await page.getByLabel(t.opportunities.form.estimatedAmount.label).fill('100')
    await page.getByRole('button', { name: t.opportunities.form.submit, exact: true }).click()
    await expect(page).toHaveURL(/\/crm\/opportunities\/new$/)
    await expect(page.getByText(t.opportunities.form.currency.invalid)).toBeVisible()

    // Invalid amount (too many decimals)
    await page.getByLabel(t.opportunities.form.currency.label).fill('USD')
    await page.getByLabel(t.opportunities.form.estimatedAmount.label).fill('100.999')
    await page.getByRole('button', { name: t.opportunities.form.submit, exact: true }).click()
    await expect(page).toHaveURL(/\/crm\/opportunities\/new$/)
    await expect(page.getByText(t.opportunities.form.estimatedAmount.decimals)).toBeVisible()

    // Verify no opportunity was created
    const afterList = await listViaApi(admin)
    expect(afterList.length).toBe(beforeList.length)
  })

  test('5: Forbidden actions with viewer account', async ({ page }) => {
    const admin = await adminToken(1)
    const viewer = await viewerToken()

    // Prepare an open opportunity via admin
    const id = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 1000 })
    const opp = await getViaApi(admin, id)
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 1)
    await openViaApi(admin, id, { expectedVersion: opp.rowVersion, expiryDate: futureDate.toISOString() })

    // Viewer tries to change stage: API returns 403
    const api = await newApi()
    const stageChange = await api.post(`/api/opportunities/${id}/stage`, {
      data: { expectedVersion: opp.rowVersion, targetStageId: 999 },
      headers: { Authorization: `Bearer ${viewer}`, ...CSRF, 'Idempotency-Key': crypto.randomUUID() },
    })
    expect(stageChange.status()).toBe(403)

    // Viewer tries to create: API returns 403
    const create = await api.post('/api/opportunities', {
      data: { partyId: 2001, currency: 'EUR', estimatedAmount: 500 },
      headers: { Authorization: `Bearer ${viewer}`, ...CSRF, 'Idempotency-Key': crypto.randomUUID() },
    })
    expect(create.status()).toBe(403)

    // Viewer tries to reassign: API returns 403
    const reassign = await api.post(`/api/opportunities/${id}/reassign`, {
      data: { expectedVersion: opp.rowVersion, newPrincipalIssuer: 'system', newPrincipalSubject: 'someone' },
      headers: { Authorization: `Bearer ${viewer}`, ...CSRF, 'Idempotency-Key': crypto.randomUUID() },
    })
    expect(reassign.status()).toBe(403)
    await api.dispose()

    // Verify in the UI that viewer has no actions
    await signIn(page, VIEWER, SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/) // viewer has no tenant selection, belongs to 1
    await page.goto(`/crm/opportunities/${id}`)

    // Assert no-actions state is visible
    await expect(page.locator('[data-testid="no-actions"]')).toBeVisible()
    await expect(page.getByRole('button', { name: t.opportunities.open.action, exact: true })).not.toBeVisible()
    await expect(page.getByRole('button', { name: t.opportunities.win.action, exact: true })).not.toBeVisible()
    await expect(page.getByRole('button', { name: t.opportunities.lose.action, exact: true })).not.toBeVisible()
  })

  test('6: Reassign — the server lists who can take over; only an authorized caller sees the control', async ({ page }) => {
    const admin = await adminToken(1)

    const id = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 800 })
    const opp = await getViaApi(admin, id)
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 1)
    await openViaApi(admin, id, { expectedVersion: opp.rowVersion, expiryDate: futureDate.toISOString() })
    const ownerBefore = (await getViaApi(admin, id)).assignedPrincipalSubject

    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto(`/crm/opportunities/${id}`)
    await page.getByRole('button', { name: t.opportunities.summary.reassign, exact: true }).click()

    const dialog = page.getByRole('dialog')
    await dialog.getByRole('combobox', { name: t.opportunities.reassign.assignee.label }).fill('Dev')
    // "Dev" matches every seeded member's name; the SERVER offers only the one who is actually assignable:
    // the viewer (cannot move a stage), the no-grant member and the current owner are not in the list.
    await expect(page.getByRole('option')).toHaveText([/Dev Sales Representative/])
    await page.getByRole('option', { name: /Dev Sales Representative/ }).click()
    await dialog.getByRole('button', { name: t.opportunities.reassign.submit, exact: true }).click()
    await expect(dialog).not.toBeVisible()

    const ownerAfter = (await getViaApi(admin, id)).assignedPrincipalSubject
    expect(ownerAfter).not.toBe(ownerBefore)
    expect(ownerAfter).toBe(subjectOf(await salesRepToken()))
    await expect(page.getByTestId('summary-owner')).toContainText(ownerAfter!)

    // The sales representative can work the record (tenant-wide read) but is offered no Reassign control, and the API agrees.
    await page.context().clearCookies()
    await signIn(page, SALES_REP, SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/) // wait for the session before navigating
    await page.goto(`/crm/opportunities/${id}`)
    await expect(page.getByText(t.opportunities.detail.title.replace('{{id}}', String(id)))).toBeVisible()
    await expect(page.getByRole('button', { name: t.opportunities.summary.reassign, exact: true })).not.toBeVisible()
    expect((await assignableViaApi(await salesRepToken(), id)).status).toBe(403)

    // The viewer: same.
    await page.context().clearCookies()
    await signIn(page, VIEWER, SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/) // wait for the session before navigating
    await page.goto(`/crm/opportunities/${id}`)
    await expect(page.getByRole('button', { name: t.opportunities.summary.reassign, exact: true })).not.toBeVisible()
    expect((await assignableViaApi(await viewerToken(), id)).status).toBe(403)
  })

  test('6b: Reassign — the server re-validates the target and never trusts a client-supplied principal', async () => {
    const admin = await adminToken(1)
    const id = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 900 })
    const opp = await getViaApi(admin, id)
    const issuer = opp.assignedPrincipalIssuer!

    // The candidate query: search narrows, and non-assignable members never appear even when they match.
    const all = await assignableViaApi(admin, id)
    expect(all.status).toBe(200)
    expect(all.body!.map((p) => p.displayName)).toEqual(['Dev Sales Representative'])
    expect((await assignableViaApi(admin, id, 'viewer')).body).toEqual([])

    // The viewer reads but cannot move a stage: naming them directly is refused by the server (422), as is a foreign issuer.
    const viewerSubject = subjectOf(await viewerToken())
    const toViewer = await reassignViaApi(admin, id, opp.rowVersion, issuer, viewerSubject)
    expect(toViewer).toEqual({ status: 422, type: 'principal_not_assignable' })
    const foreign = await reassignViaApi(admin, id, opp.rowVersion, 'https://someone-else.example', subjectOf(await salesRepToken()))
    expect(foreign).toEqual({ status: 422, type: 'principal_not_assignable' })
    expect((await getViaApi(admin, id)).assignedPrincipalSubject).toBe(opp.assignedPrincipalSubject) // nothing changed

    // Tenant isolation: another tenant's administrator cannot reach this record's candidates at all.
    const otherTenant = await assignableViaApi(await adminToken(2), id)
    expect(otherTenant.status).toBe(404)
  })

  test('6c: Party reference query — tenant-scoped, permission-gated, and the picker fails closed', async ({ page }) => {
    const admin1 = await adminToken(1)
    const admin2 = await adminToken(2)

    // Tenant 1 sees its own customers; tenant 2's are not reachable by name or by id.
    const own = await searchPartiesViaApi(admin1, { search: 'acme' })
    expect(own.body!.map((p) => p.displayName)).toEqual(['Acme Corporation'])
    expect((await searchPartiesViaApi(admin1, { search: 'umbrella' })).body).toEqual([])
    const foreign = await searchPartiesViaApi(admin2, { search: 'umbrella' })
    expect(foreign.body!.map((p) => p.displayName)).toEqual(['Umbrella Holdings'])
    expect((await searchPartiesViaApi(admin2, { ids: String(own.body![0].id) })).body).toEqual([])

    // A read-only member has no party-search permission: the API refuses, and the form's picker says so instead of falling back to a typed id.
    expect((await searchPartiesViaApi(await viewerToken(), { search: 'acme' })).status).toBe(403)
    expect((await searchPartiesViaApi(await salesRepToken(), { search: 'acme' })).status).toBe(200)

    await signIn(page, VIEWER, SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/opportunities/new')
    await page.getByRole('combobox', { name: t.opportunities.form.partyId.label }).fill('acme')
    await expect(page.getByText(t.opportunities.picker.forbidden)).toBeVisible()
    await expect(page.getByRole('textbox', { name: t.opportunities.form.partyId.label })).toHaveCount(0)
  })

  test('7: Field READ/WRITE restrictions - not applicable: Phase 1.5 has no field-level security (plan gap G7)', async () => {
    // This test is intentionally skipped: Phase 1.5 does not implement field-level security.
    // The matrix row is documented here for traceability of that gap.
  })

  test('8: Concurrency — conflict detection and reload', async ({ browser }) => {
    const admin = await adminToken(1)

    const id = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 600 })

    const contextA = await browser.newContext({ locale: 'tr-TR' })
    const contextB = await browser.newContext({ locale: 'tr-TR' })
    const pageA = await contextA.newPage()
    const pageB = await contextB.newPage()

    // Both sign in and navigate to the detail page
    await signIn(pageA, ADMIN, SEED_PASSWORD)
    await pageA.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(pageA).toHaveURL(/\/dashboard$/)
    await pageA.goto(`/crm/opportunities/${id}`)

    await signIn(pageB, ADMIN, SEED_PASSWORD)
    await pageB.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(pageB).toHaveURL(/\/dashboard$/)
    await pageB.goto(`/crm/opportunities/${id}`)

    // Context B adds a line (bumps version)
    const oppB = await getViaApi(admin, id)
    await addLineViaApi(admin, id, { expectedVersion: oppB.rowVersion, productId: 90, quantity: 1, unitPrice: 100, isOptional: false })

    // Context A tries to open with stale version
    // Open dialog
    await pageA.getByRole('button', { name: t.opportunities.open.action, exact: true }).click()
    await pageA.getByRole('dialog').getByRole('button', { name: t.opportunities.open.submit, exact: true }).click()

    // Concurrency alert appears
    const problem = pageA.locator('[role="alert"][data-problem="concurrency"]')
    await expect(problem).toBeVisible()

    // Verify server state is unchanged
    const oppAfterConflict = await getViaApi(admin, id)
    expect(oppAfterConflict.status).toBe(0) // Draft

    // Click "Reload latest" button
    const reloadBtn = pageA.getByRole('button', { name: t.opportunities.problem.reload, exact: true })
    await expect(reloadBtn).toBeVisible()
    await reloadBtn.click()

    // Lines table should now show the line B added
    // B's line is now on A's page (a Draft that had none has exactly this one).
    // (CSS locator: the open dialog makes the page behind it aria-hidden, which role queries exclude.)
    await expect(pageA.locator('table tbody tr')).toHaveCount(1)

    // The dialog is still open with the user's input; submitting again is an explicit act and now carries the fresh version.
    await pageA.getByRole('dialog').getByRole('button', { name: t.opportunities.open.submit, exact: true }).click()

    // Should succeed this time
    await expect(pageA.locator('role=dialog')).not.toBeVisible()
    const oppAfterSuccess = await getViaApi(admin, id)
    expect(oppAfterSuccess.status).toBe(1) // Open

    await contextA.close()
    await contextB.close()
  })

  test('9a: Idempotency — double-click create form in UI', async ({ page }) => {
    const admin = await adminToken(1)

    const beforeList = await listViaApi(admin)

    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/opportunities')
    await page.getByRole('button', { name: t.opportunities.list.newAction, exact: true }).first().click()

    await choosePartyByName(page, 'Initech', 'Initech')
    await page.getByLabel(t.opportunities.form.currency.label).fill('EUR')
    await page.getByLabel(t.opportunities.form.estimatedAmount.label).fill('1000')

    // Double-click the submit button quickly
    const submitBtn = page.getByRole('button', { name: t.opportunities.form.submit, exact: true })
    await submitBtn.dblclick()

    // Wait for navigation
    await expect(page).toHaveURL(/\/crm\/opportunities\/\d+$/)

    // Verify exactly one new opportunity was created
    const afterList = await listViaApi(admin)
    expect(afterList.length).toBe(beforeList.length + 1)
  })

  test('9a-2: creating an opportunity for a customer that does not exist is a 422 party_not_found', async () => {
    const admin = await adminToken(1)
    const api = await newApi()

    const response = await api.post('/api/opportunities', {
      data: { partyId: 987654321, currency: 'EUR', estimatedAmount: 10 },
      headers: { Authorization: `Bearer ${admin}`, ...CSRF, 'Idempotency-Key': crypto.randomUUID() },
    })

    expect(response.status()).toBe(422)
    expect(((await response.json()) as { type: string }).type).toBe('party_not_found')
    await api.dispose()
  })

  test('9b: Idempotency — same key with same body vs. different body', async () => {
    const admin = await adminToken(1)

    const api = await newApi()

    const idempotencyKey = crypto.randomUUID()
    const body1 = { partyId: await seededPartyId(admin), currency: 'EUR', estimatedAmount: 2000 }

    // First request
    const response1 = await api.post('/api/opportunities', {
      data: body1,
      headers: { Authorization: `Bearer ${admin}`, ...CSRF, 'Idempotency-Key': idempotencyKey },
    })
    expect(response1.status()).toBe(201)
    const result1 = (await response1.json()) as { opportunityId: number; replayed: boolean }
    const id = result1.opportunityId
    expect(result1.replayed).toBe(false)

    // Same key, same body → replayed
    const response2 = await api.post('/api/opportunities', {
      data: body1,
      headers: { Authorization: `Bearer ${admin}`, ...CSRF, 'Idempotency-Key': idempotencyKey },
    })
    expect(response2.status()).toBe(201) // a replay answers like the original
    const result2 = (await response2.json()) as { opportunityId: number; replayed: boolean }
    expect(result2.opportunityId).toBe(id)
    expect(result2.replayed).toBe(true)

    // Same key, different body → 409 idempotency_key_reused
    const response3 = await api.post('/api/opportunities', {
      data: { ...body1, currency: 'GBP', estimatedAmount: 3000 },
      headers: { Authorization: `Bearer ${admin}`, ...CSRF, 'Idempotency-Key': idempotencyKey },
    })
    expect(response3.status()).toBe(409)
    const error = (await response3.json()) as { type: string }
    expect(error.type).toBe('idempotency_key_reused')

    // List count should be +1 only
    const list = await listViaApi(admin)
    const count = list.filter((opp) => opp.id === id).length
    expect(count).toBe(1)

    await api.dispose()
  })

  test('10: Tenant isolation — opportunity disappears on tenant switch', async ({ page }) => {
    const admin = await adminToken(1)
    const admin2 = await adminToken(2)

    // Create in tenant 1
    const id = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 5000 })

    // Verify visible in tenant 1
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/opportunities')
    const rows = page.locator('[data-testid="opportunity-row"]')
    await expect(rows.first()).toBeVisible()

    // Find the row with our id
    const ourRow = page.locator(`[data-testid="opportunity-row"] a:has-text("#${id}")`)
    await expect(ourRow).toBeVisible()

    // Switch to tenant 2
    await openUserMenu(page)
    const tenantTwo = page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '2') })
    // Switching inside the app keeps the page; wait for the server to have issued the tenant-2 token.
    await Promise.all([page.waitForResponse((response) => response.url().includes('/auth/tenants/select') && response.ok()), tenantTwo.click()])

    // Navigate to opportunities
    await page.goto('/crm/opportunities')

    // The row should be gone
    await expect(ourRow).not.toBeVisible()

    // Try to open the detail URL directly
    await page.goto(`/crm/opportunities/${id}`)

    // Should show not-found state
    await expect(page.getByText(t.opportunities.state.notFound.title)).toBeVisible()

    // Verify via API with tenant-2 token that it's 404
    const api = await newApi()
    const response = await api.get(`/api/opportunities/${id}`, {
      headers: { Authorization: `Bearer ${admin2}` },
    })
    expect(response.status()).toBe(404)
    // A read of a record in another tenant is answered exactly like a read of an id that never existed.
    const missing = await api.get('/api/opportunities/999999', { headers: { Authorization: `Bearer ${admin2}` } })
    expect(missing.status()).toBe(404)
    expect(await response.text()).toBe(await missing.text())
    expect([...Object.keys(response.headers())].filter((name) => name.startsWith('x-')).sort()).toEqual(
      [...Object.keys(missing.headers())].filter((name) => name.startsWith('x-')).sort(),
    )
    await api.dispose()

    // Also verify that a nonexistent id (999999) shows the same state
    await page.goto('/crm/opportunities/999999')
    await expect(page.getByText(t.opportunities.state.notFound.title)).toBeVisible()
  })

  test('11: 401 recovery — mid-session token refresh on list refetch', async ({ page }) => {
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/opportunities')

    // Set up a route interceptor that answers the FIRST GET to /api/opportunities (not /api/opportunities/{id})
    let interceptCount = 0
    await page.route('**/api/opportunities', async (route) => {
      const req = route.request()
      const url = req.url()
      // Only intercept GET requests to the list endpoint (not detail or actions)
      if (req.method() === 'GET' && url.endsWith('/opportunities') && !url.includes('/actions')) {
        if (interceptCount++ === 0) {
          await route.fulfill({
            status: 401,
            contentType: 'application/json',
            body: JSON.stringify({ status: 401, type: 'unauthorized', title: 'Unauthorized' }),
          })
          return
        }
      }
      await route.continue()
    })

    // Trigger a refetch via status filter
    const segments = page.locator('[role="tablist"] button')
    await segments.nth(1).click() // Click a status filter tab to refetch

    // The page should still render with the list
    await expect(page.getByRole('heading', { name: t.opportunities.list.title })).toBeVisible()

    // URL should NOT be /login
    expect(page.url()).not.toContain('/login')

    await expectNoTokenInStorage(page)
  })

  test('12a: no CRM grant — the list is empty (fail-closed scope) and a record is indistinguishable from a missing one', async ({ page }) => {
    const admin = await adminToken(1)
    const id = await createViaApi(admin, { currency: 'EUR', estimatedAmount: 10 })

    await signIn(page, SINGLE, SEED_PASSWORD)
    await expect(page).toHaveURL(/\/dashboard$/)

    // Reads are scoped, not refused: the list answers 200 with nothing in it.
    await page.goto('/crm/opportunities')
    await expect(page.getByText(t.opportunities.list.empty.title)).toBeVisible()

    // The API answers a denied read exactly like a missing record (404), so the UI must not tell them apart.
    await page.goto(`/crm/opportunities/${id}`)
    await expect(page.getByText(t.opportunities.state.notFound.title)).toBeVisible()
    await expect(page.getByText(t.opportunities.state.forbidden.title)).toHaveCount(0)
  })

  test('12b: Not-found state — admin on nonexistent opportunity', async ({ page }) => {
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)

    await page.goto('/crm/opportunities/999999')

    await expect(page.getByText(t.opportunities.state.notFound.title)).toBeVisible()
  })

  test('12c: Malformed ID returns not-found state', async ({ page }) => {
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)

    await page.goto('/crm/opportunities/abc')

    await expect(page.getByText(t.opportunities.state.notFound.title)).toBeVisible()
  })
})
