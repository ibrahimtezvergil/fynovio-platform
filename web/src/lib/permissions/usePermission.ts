import { useSessionStore } from '@/lib/auth'
import type { PermissionId, PermissionPolicy, PermissionUser } from './types'

/**
 * UX ONLY — this controls what the client renders, never authorization. A
 * backend must enforce every permission independently for every request.
 *
 * Remnant of the mock era: the real session carries no role (the API decides
 * per request), so with a real sign-in this policy grants NOTHING — it fails
 * closed. Capabilities served by the backend replace it in Phase 2.5B; until
 * then it only gates the pipeline demo's approve action.
 */
export const mockPermissionPolicy: PermissionPolicy = {
  admin: ['*'],
  manager: ['deal.change-stage', 'deal.approve'],
  viewer: [],
}

/** Resolve a mock policy without tying callers to the session store. No role ⇒ no permissions. */
export function hasPermission(
  user: PermissionUser | null | undefined,
  permission: PermissionId,
  policy: PermissionPolicy = mockPermissionPolicy,
): boolean {
  if (!user?.role) return false
  const permissions = policy[user.role]
  return permissions.includes('*') || permissions.includes(permission)
}

/** Reactive UX predicate for the signed-in user. */
export function usePermission(permission: PermissionId): boolean {
  return useSessionStore((state) => hasPermission(state.user as PermissionUser | null, permission))
}
