import { useMutation } from '@tanstack/react-query'
import type { LoginValues } from '@/features/auth/schema'
import { login, selectTenant, type SessionStatus } from '@/lib/auth'
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
