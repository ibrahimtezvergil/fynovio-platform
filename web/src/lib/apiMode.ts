/**
 * Which backend the running app talks to, stated in one place so a developer can tell at a glance
 * (Phase 2.5B spec §20). MSW only ever answers the mock-era features (deals, dashboards, ...); the
 * Opportunity, pipeline-stage and Calendar APIs are never mocked — a vitest guard enforces that.
 */
export const apiMode = {
  baseUrl: import.meta.env.VITE_API_URL || '/api',
  mockingEnabled: import.meta.env.VITE_API_MOCKING !== 'disabled',
} as const
