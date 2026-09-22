import { defineConfig } from '@playwright/test'

// Driven by scripts/e2e.sh, which provides the throwaway PostgreSQL (E2E_PG_PORT) and builds the API first.
// Ports default to values that do not collide with a normal dev setup (5173 / 5208); set on process.env so the
// test workers (e2e/support/env.ts) see the same values as this file.
process.env.E2E_APP_PORT ??= '5174'
process.env.E2E_API_PORT ??= '5209'
const APP_URL = `http://localhost:${process.env.E2E_APP_PORT}`
const API_URL = `http://localhost:${process.env.E2E_API_PORT}`
const PG_PORT = process.env.E2E_PG_PORT ?? '55432'
const RUNTIME_CONNECTION = `Host=localhost;Port=${PG_PORT};Database=fynovio_platform;Username=fynovio_app;Password=runtime`

/** Shared with the tests (e2e/support/env.ts reads the same variable). */
const SEED_PASSWORD = process.env.E2E_SEED_PASSWORD ?? 'E2E-Seed-Passw0rd-1234'

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts', // not *.test.ts / *.spec.ts: vitest owns those
  // One database, one set of rate-limit counters, seeded identities: run serially.
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],
  outputDir: 'test-results',
  use: {
    baseURL: APP_URL,
    // The installed Chrome: no browser download. `E2E_BROWSER_CHANNEL=chromium` after `npx playwright install chromium`.
    channel: process.env.E2E_BROWSER_CHANNEL === 'chromium' ? undefined : (process.env.E2E_BROWSER_CHANNEL ?? 'chrome'),
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'tr-TR',
  },
  webServer: [
    {
      // Built by scripts/e2e.sh. Development environment (dev mailbox, dev seed); never sends real mail.
      command: 'dotnet run --project ../src/Host --no-build --no-launch-profile',
      url: `${API_URL}/health/db`,
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: API_URL,
        // The browser is served from APP_URL: CORS/CSRF origin checks and e-mailed links must use it.
        Authentication__AllowedOrigins__0: APP_URL,
        Authentication__PublicAppBaseUrl: APP_URL,
        ConnectionStrings__Access: RUNTIME_CONNECTION,
        ConnectionStrings__Crm: RUNTIME_CONNECTION,
        ConnectionStrings__Collaboration: RUNTIME_CONNECTION,
        ConnectionStrings__MasterData: RUNTIME_CONNECTION,
        ConnectionStrings__TenantLifecycle: RUNTIME_CONNECTION,
        DevSeed__Enabled: 'true',
        DevSeed__Password: SEED_PASSWORD,
        Email__Smtp__Enabled: 'false',
        // The suite signs in constantly from one address: lift the throttles (they have their own unit/HTTP tests).
        Authentication__RateLimiting__LoginPerMinute: '10000',
        Authentication__RateLimiting__RefreshPerMinute: '10000',
        Authentication__RateLimiting__ForgotPerHour: '10000',
        Authentication__RateLimiting__TokenPer15Minutes: '10000',
        Authentication__RateLimiting__PasswordPer15Minutes: '10000',
        Authentication__RateLimiting__PublicPerMinute: '10000',
        Authentication__RateLimiting__LoginPerIdentifierPerMinute: '10000',
        Authentication__RateLimiting__ForgotPerIdentifierPerHour: '10000',
      },
    },
    {
      command: `npm run dev -- --port ${process.env.E2E_APP_PORT} --strictPort`,
      url: APP_URL,
      timeout: 120_000,
      reuseExistingServer: false,
      env: { API_PROXY_TARGET: API_URL },
    },
  ],
})
