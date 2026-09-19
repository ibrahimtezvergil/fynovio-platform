import axios, { AxiosError, type AxiosInstance } from 'axios'
import type { ApiError } from '@/types'

/**
 * Base axios instance. No backend yet — this exists so that when one arrives,
 * the only thing that changes is VITE_API_URL.
 */
export const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})

let authToken: string | null = null

export function setAuthToken(token: string | null) {
  authToken = token
}

apiClient.interceptors.request.use((config) => {
  if (authToken) {
    config.headers.Authorization = `Bearer ${authToken}`
  }
  return config
})

/** Collapse every axios failure into one predictable shape. */
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<{ message?: string; code?: string; errors?: Record<string, string[]> }>) => {
    const normalized: ApiError = {
      message: error.response?.data?.message ?? error.message ?? 'Beklenmeyen bir hata oluştu.',
      status: error.response?.status ?? 0,
      code: error.response?.data?.code,
      // Laravel's ValidationException 422 body — see ApiError.fields.
      fields: error.response?.status === 422 ? error.response.data?.errors : undefined,
    }
    return Promise.reject(normalized)
  },
)
