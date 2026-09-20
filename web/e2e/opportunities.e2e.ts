import { expect, test } from '@playwright/test'
import { adminToken, newApi } from './support/api.ts'
import { ADMIN, SEED_PASSWORD, VIEWER, SINGLE } from './support/env.ts'
import { expectNoTokenInStorage, openUserMenu, signIn, t } from './support/ui.ts'
import {
  addLineViaApi,
  createViaApi,
  getViaApi,
  listViaApi,
  openViaApi,
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

    // Create opportunity: party 1001, EUR, 250
    await page.getByLabel(t.opportunities.form.partyId.label).fill('1001')
    await page.getByLabel(t.opportunities.form.currency.label).fill('EUR')
    await page.getByLabel(t.opportunities.form.estimatedAmount.label).fill('250')
    await page.getByRole('button', { name: t.opportunities.form.submit, exact: true }).click()

    // Lands on detail page in Draft status
    await expect(page).toHaveURL(/\/crm\/opportunities\/\d+$/)
    const opportunityId = Number(page.url().split('/').pop())
    await expect(page.getByText(t.opportunities.status.Draft)).toBeVisible()

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
    const id = await createViaApi(admin, { partyId: 1002, currency: 'USD', estimatedAmount: 500 })
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

    const id = await createViaApi(admin, { partyId: 1003, currency: 'GBP', estimatedAmount: 300 })
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
    await page.getByLabel(t.opportunities.form.partyId.label).fill('999')
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
    const id = await createViaApi(admin, { partyId: 2000, currency: 'EUR', estimatedAmount: 1000 })
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

  test('6: Reassign dependency visible for authorized users only', async ({ page }) => {
    const admin = await adminToken(1)

    const id = await createViaApi(admin, { partyId: 2002, currency: 'EUR', estimatedAmount: 800 })
    const opp = await getViaApi(admin, id)
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 1)
    await openViaApi(admin, id, { expectedVersion: opp.rowVersion, expiryDate: futureDate.toISOString() })

    // Admin: sees reassign dependency
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: t.auth.tenantSelector.tenantLabel.replace('{{id}}', '1') }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto(`/crm/opportunities/${id}`)
    await expect(page.locator('[data-testid="reassign-dependency"]')).toBeVisible()

    // Viewer: does not see it
    await page.context().clearCookies()
    await signIn(page, VIEWER, SEED_PASSWORD)
    await page.goto(`/crm/opportunities/${id}`)
    await expect(page.locator('[data-testid="reassign-dependency"]')).not.toBeVisible()
  })

  test('7: Field READ/WRITE restrictions - not applicable: Phase 1.5 has no field-level security (plan gap G7)', async () => {
    // This test is intentionally skipped: Phase 1.5 does not implement field-level security.
    // The matrix row is documented here for traceability of that gap.
  })

  test('8: Concurrency — conflict detection and reload', async ({ browser }) => {
    const admin = await adminToken(1)

    const id = await createViaApi(admin, { partyId: 2003, currency: 'EUR', estimatedAmount: 600 })

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

    await page.getByLabel(t.opportunities.form.partyId.label).fill('3000')
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

  test('9b: Idempotency — same key with same body vs. different body', async () => {
    const admin = await adminToken(1)

    const api = await newApi()

    const idempotencyKey = crypto.randomUUID()
    const body1 = { partyId: 3001, currency: 'EUR', estimatedAmount: 2000 }

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
      data: { partyId: 3002, currency: 'GBP', estimatedAmount: 3000 },
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
    const id = await createViaApi(admin, { partyId: 4000, currency: 'EUR', estimatedAmount: 5000 })

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
    const id = await createViaApi(admin, { partyId: 5000, currency: 'EUR', estimatedAmount: 10 })

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
