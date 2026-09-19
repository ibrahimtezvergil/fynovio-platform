/**
 * Must mirror `apiClient`'s `baseURL` (`src/api/client.ts`) exactly — MSW
 * matches request paths as given, it does not know about axios' `baseURL`.
 */
export const API_BASE = import.meta.env.VITE_API_URL || '/api'
