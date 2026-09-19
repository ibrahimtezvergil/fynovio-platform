import type { User, UserRole } from '@/types'

/** A stable action identifier, supplied by the backend policy contract later. */
export type PermissionId = string

/**
 * Temporary client-side policy shape. The backend will eventually resolve this
 * per authenticated user and tenant; roles are only the current mock input.
 */
export type PermissionPolicy = Readonly<Record<UserRole, readonly PermissionId[]>>

export type PermissionUser = Pick<User, 'role'>
