/** Set by playwright.config.ts (which also starts the servers on it). */
export const APP_URL = `http://localhost:${process.env.E2E_APP_PORT ?? '5174'}`

export const SEED_PASSWORD = process.env.E2E_SEED_PASSWORD ?? 'E2E-Seed-Passw0rd-1234'

/** The seeded identities (src/Host/Authentication/DevSeeder.cs). */
export const ADMIN = 'admin@fynovio.local' // tenant administrator of tenants 1 and 2
export const SINGLE = 'single@fynovio.local' // member of tenant 1, no grants
export const VIEWER = 'viewer@fynovio.local' // member of tenant 1, crm.opportunity.read + list only
export const SALES_REP = 'rep@fynovio.local' // member of tenant 1, CRM sales representative: read + work opportunities, no reassign
export const NO_MEMBERSHIP = 'nomember@fynovio.local'

export const NEW_PASSWORD = 'E2E-Fresh-Passw0rd-5678'
export const OTHER_PASSWORD = 'E2E-Another-Passw0rd-9012'
