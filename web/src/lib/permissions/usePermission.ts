import { useAuthStore } from '@/features/auth/store/useAuthStore'
import type { PermissionId, PermissionPolicy, PermissionUser } from './types'

/**
 * UX ONLY — this controls what the client renders, never authorization. A
 * backend must enforce every permission independently for every request.
 */
export const mockPermissionPolicy: PermissionPolicy = {
  admin: ['*'],
  manager: ['deal.change-stage', 'deal.approve'],
  viewer: [],
}

/** Resolve a mock policy without tying callers to the current auth store. */
export function hasPermission(
  user: PermissionUser | null | undefined,
  permission: PermissionId,
  policy: PermissionPolicy = mockPermissionPolicy,
): boolean {
  if (!user) return false
  const permissions = policy[user.role]
  return permissions.includes('*') || permissions.includes(permission)
}

/** Reactive UX predicate for the signed-in mock user. */
export function usePermission(permission: PermissionId): boolean {
  return useAuthStore((state) => hasPermission(state.user, permission))
}
