import { expect, test } from '@playwright/test'
import companySettings from '../src/locales/tr/company-settings.ts'
import { adminToken, newApi } from './support/api.ts'
import { ADMIN, SEED_PASSWORD } from './support/env.ts'
import { signIn, tenantLabel } from './support/ui.ts'

interface CompanySettings {
  displayName: string
  legalName: string | null
  taxNumber: string | null
  taxOffice: string | null
  email: string | null
  phone: string | null
  address: string | null
  timezone: string
  currencyCode: string
  rowVersion: number
}

const headers = (token: string, key?: string) => ({
  Authorization: `Bearer ${token}`,
  'X-Requested-With': 'fynovio',
  ...(key ? { 'Idempotency-Key': key } : {}),
})

test.describe('Company settings — real API', () => {
  test('tenant administrator reads and updates the provisioned profile through the browser', async ({ page }) => {
    const token = await adminToken(1)
    const api = await newApi()
    const current = await api.get('/api/company/settings', { headers: headers(token) })
    expect(current.ok()).toBe(true)
    const original = await current.json() as CompanySettings
    const displayName = `E2E Company ${Date.now().toString(36)}`

    try {
      await signIn(page, ADMIN, SEED_PASSWORD)
      await page.getByRole('button', { name: tenantLabel(1) }).click()
      await page.goto('/company/settings')
      await expect(page.getByRole('heading', { name: companySettings.page.title, level: 1 })).toBeVisible()
      const field = page.getByLabel(companySettings.identity.displayName, { exact: true })
      await expect(field).toHaveValue(original.displayName)
      await field.fill(displayName)
      await page.getByRole('button', { name: companySettings.saveBar.save }).click()
      await expect(page.getByText(companySettings.saveBar.allSaved)).toBeVisible()

      const changed = await api.get('/api/company/settings', { headers: headers(token) })
      expect(changed.ok()).toBe(true)
      expect((await changed.json() as CompanySettings).displayName).toBe(displayName)
    } finally {
      // Restore shared seeded state so this test does not become an order dependency for the rest of the suite.
      const latest = await api.get('/api/company/settings', { headers: headers(token) })
      if (latest.ok()) {
        const values = await latest.json() as CompanySettings
        await api.put('/api/company/settings', {
          data: { ...original, expectedVersion: values.rowVersion },
          headers: headers(token, `company-settings-restore-${Date.now().toString(36)}`),
        })
      }
      await api.dispose()
    }
  })
})
