import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { endpoints } from '@/api/endpoints'
import { getAccessToken } from '@/lib/auth/session'
import type { ApiError } from '@/types'

declare module 'axios' {
  interface AxiosRequestConfig {
    /** Auth-endpoint calls set this: a 401 there is an answer, never a reason to refresh. */
    skipAuthRefresh?: boolean
    /** Set once a request has been replayed after a refresh — a second 401 is final. */
    _retried?: boolean
  }
}

/** Base axios instance. The refresh cookie travels with it (`withCredentials`); the access token is added per request. */
export const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

/**
 * Registered by `lib/auth/sessionClient` (which itself uses this client, so the
 * dependency can only point one way). Resolves true when a NEW access token was obtained.
 */
type RefreshHandler = () => Promise<boolean>
let refreshHandler: RefreshHandler | null = null

export function setRefreshHandler(handler: RefreshHandler | null) {
  refreshHandler = handler
}

/** `/auth/*` calls that authenticate with the bearer token (not the cookie or a link token): an expired token there IS worth a refresh. */
const BEARER_AUTH_ENDPOINTS: readonly string[] = [endpoints.auth.me, endpoints.auth.changePassword]

const isAuthEndpoint = (url: string | undefined) => (url ?? '').startsWith('/auth/') && !BEARER_AUTH_ENDPOINTS.includes(url ?? '')

function newCorrelationId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`
}

apiClient.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  // A custom header is what makes a cross-origin state-changing call preflighted; the server requires it (CSRF guard).
  if (config.method && !['get', 'head', 'options'].includes(config.method.toLowerCase())) {
    config.headers['X-Requested-With'] = 'fynovio'
  }
  config.headers['X-Correlation-Id'] = newCorrelationId()
  return config
})

interface ProblemBody {
  type?: string
  title?: string
  detail?: string
  message?: string
  code?: string
  errors?: Record<string, string[]>
  codes?: Record<string, string[]>
  violations?: string[]
}

/** Collapse every axios failure — RFC 7807 ProblemDetails, the older `{message,code}` shape, network errors — into one `ApiError`. */
export function toApiError(error: AxiosError<ProblemBody>): ApiError {
  const data = error.response?.data
  const status = error.response?.status ?? 0
  const retryAfter = Number(error.response?.headers?.['retry-after'])

  return {
    message: data?.detail ?? data?.message ?? data?.title ?? error.message ?? 'Beklenmeyen bir hata oluştu.',
    status,
    // ProblemDetails carries the machine-readable code in `type` (our API uses short snake_case codes).
    code: data?.code ?? data?.type,
    // Laravel-style 422 and our 400 `validation_error` share the `errors` map.
    fields: status === 422 || status === 400 ? data?.errors : undefined,
    ...(status === 422 && data?.codes ? { fieldCodes: data.codes } : {}),
    ...(status === 400 && Array.isArray(data?.violations) ? { violations: data.violations } : {}),
    ...(status === 429 && Number.isFinite(retryAfter) ? { retryAfterSeconds: retryAfter } : {}),
  }
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemBody>) => {
    const config = error.config as InternalAxiosRequestConfig | undefined

    if (
      error.response?.status === 401 &&
      config &&
      refreshHandler &&
      !config.skipAuthRefresh &&
      !config._retried &&
      !isAuthEndpoint(config.url)
    ) {
      config._retried = true
      if (await refreshHandler()) return apiClient(config) // the request interceptor attaches the new token
    }

    return Promise.reject(toApiError(error))
  },
)
