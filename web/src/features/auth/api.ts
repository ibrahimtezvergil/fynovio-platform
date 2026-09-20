import { useMutation, useQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import type { LoginValues } from '@/features/auth/schema'
import { DEFAULT_POLICY, type PasswordPolicyHints } from '@/features/auth/lib/passwordPolicy'
import { acceptInvitation, login, resetPassword, selectTenant, type SessionStatus } from '@/lib/auth'
import type { ApiError } from '@/types'

/**
 * Session mutations use the plain `useMutation` on purpose (not `useAppMutation`):
 * they are not data writes — nothing to invalidate, no success toast, and their
 * failures render inline (a toast would echo server wording for a sign-in
 * attempt). The session store, not the mutation, holds who is signed in.
 */
export function useLogin() {
  return useMutation<SessionStatus, ApiError, LoginValues>({
    mutationKey: ['auth', 'login'],
    mutationFn: (values) => login(values),
  })
}

export function useSelectTenant() {
  return useMutation<void, ApiError, number>({
    mutationKey: ['auth', 'select-tenant'],
    mutationFn: (tenantId) => selectTenant(tenantId),
  })
}

const authConfigSchema = z.object({
  selfRegistrationEnabled: z.boolean(),
  passwordPolicy: z.object({ minLength: z.number(), maxLength: z.number() }),
})
export type AuthConfig = z.infer<typeof authConfigSchema>

/**
 * The server's own answer to "may I sign up?" and "what does a password have to look like?" — the UI only
 * mirrors it (hints, the invite-only screen); the server enforces both. Public and unauthenticated.
 */
export function useAuthConfig() {
  const query = useQuery<AuthConfig, ApiError>({
    queryKey: ['auth', 'config'],
    queryFn: async () => authConfigSchema.parse((await apiClient.get<unknown>(endpoints.auth.config)).data),
    staleTime: 5 * 60_000,
    retry: false,
    throwOnError: false,
  })
  return { ...query, policy: (query.data?.passwordPolicy ?? DEFAULT_POLICY) as PasswordPolicyHints }
}

/** What a still-valid invitation looks like to its holder (the address is masked by the server). */
const invitationPreviewSchema = z.object({
  valid: z.literal(true),
  email: z.string(),
  accountHasCredential: z.boolean(),
  displayName: z.string().nullish(),
  expiresAt: z.string().nullish(),
})
export type InvitationPreview = z.infer<typeof invitationPreviewSchema>

/** Read-only: does not consume the invitation. `error` is an `ApiError`; a 400 means "invalid, expired or used". */
export function useInvitationPreview(token: string | null) {
  return useQuery<InvitationPreview, ApiError>({
    queryKey: ['auth', 'invitation-preview', token],
    enabled: token !== null,
    queryFn: async () =>
      invitationPreviewSchema.parse((await apiClient.post<unknown>(endpoints.auth.validateInvitation, { token }, { skipAuthRefresh: true })).data),
    retry: false,
    gcTime: 0, // the token must not linger in the query cache
    staleTime: Infinity,
    throwOnError: false,
  })
}

export function useAcceptInvitation() {
  return useMutation<SessionStatus, ApiError, { token: string; password: string; displayName?: string }>({
    mutationKey: ['auth', 'accept-invitation'],
    mutationFn: (values) => acceptInvitation(values),
  })
}

export function useForgotPassword() {
  return useMutation<void, ApiError, { email: string }>({
    mutationKey: ['auth', 'forgot-password'],
    mutationFn: async (values) => {
      await apiClient.post(endpoints.auth.forgotPassword, values, { skipAuthRefresh: true })
    },
  })
}

export function useResetPassword() {
  return useMutation<void, ApiError, { token: string; newPassword: string }>({
    mutationKey: ['auth', 'reset-password'],
    mutationFn: (values) => resetPassword(values),
  })
}

export function useChangePassword() {
  return useMutation<void, ApiError, { currentPassword: string; newPassword: string }>({
    mutationKey: ['auth', 'change-password'],
    mutationFn: async (values) => {
      await apiClient.post(endpoints.auth.changePassword, values)
    },
  })
}

export function useRegister() {
  return useMutation<void, ApiError, { email: string; displayName: string; password: string }>({
    mutationKey: ['auth', 'register'],
    mutationFn: async (values) => {
      await apiClient.post(endpoints.auth.register, values, { skipAuthRefresh: true })
    },
  })
}
