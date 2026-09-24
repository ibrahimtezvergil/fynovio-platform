import { expect, test } from '@playwright/test'
import crm from '../src/locales/tr/opportunities.ts'
import { adminToken, newApi } from './support/api.ts'
import { ADMIN, SEED_PASSWORD } from './support/env.ts'
import { signIn, tenantName } from './support/ui.ts'

const headers = (token: string, key?: string) => ({ Authorization: `Bearer ${token}`, 'X-Requested-With': 'fynovio', ...(key ? { 'Idempotency-Key': key } : {}) })

test('CRM settings create a tenant opportunity type through the browser and persist it', async ({ page }) => {
  const token = await adminToken(1)
  const api = await newApi()
  const name = `E2E opportunity type ${Date.now().toString(36)}`
  const key = `e2e-${Date.now().toString(36)}`
  try {
    await signIn(page, ADMIN, SEED_PASSWORD)
    await page.getByRole('button', { name: tenantName(1) }).click()
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/crm/settings')
    await expect(page.getByRole('heading', { name: crm.settings.title })).toBeVisible()
    await page.getByRole('button', { name: crm.settings.add, exact: true }).first().click()
    await page.getByLabel(crm.settings.name, { exact: true }).fill(name)
    await page.getByLabel(crm.settings.key, { exact: true }).fill(key)
    await page.getByRole('button', { name: crm.settings.save, exact: true }).last().click()

    const response = await api.get('/api/crm/settings', { headers: headers(token) })
    expect(response.ok()).toBe(true)
    const actual = await response.json() as { opportunityTypes: { key: string; name: string; status: string }[] }
    expect(actual.opportunityTypes).toContainEqual(expect.objectContaining({ key, name, status: 'Active' }))
    await expect(page.getByText(name)).toBeVisible()
  } finally {
    await api.dispose()
  }
})
