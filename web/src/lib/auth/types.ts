import { z } from 'zod'

/**
 * Where the browser's session stands. Never persisted: it is rebuilt at every
 * page load from the HttpOnly refresh cookie (see `bootstrapSession`).
 * - `unknown`            nothing resolved yet — protected content must not render
 * - `unauthenticated`    no session
 * - `tenant_unresolved`  signed in, but no active tenant (selection needed, or no membership at all)
 * - `authenticated`      signed in with an active tenant and an access token
 * - `expired`            a session that existed and can no longer be refreshed
 */
export type SessionStatus = 'unknown' | 'unauthenticated' | 'tenant_unresolved' | 'authenticated' | 'expired'

/** What the shell shows about the signed-in person. The backend sends no role (authorization is server-side). */
export interface SessionUser {
  id: string
  name: string
  email: string
  initials: string
  locale: string | null
}

export interface Membership {
  tenantId: number
  displayName: string
}

const tenantReferenceSchema = z.object({ tenantId: z.number() })
const membershipSchema = tenantReferenceSchema.extend({ displayName: z.string() })

/** Shared response of `POST /auth/login` and `POST /auth/refresh`. */
export const authResultSchema = z.object({
  status: z.enum(['authenticated', 'tenant_selection_required', 'no_membership']),
  accessToken: z.string().nullish(),
  expiresIn: z.number().nullish(),
  account: z.object({
    id: z.number(),
    email: z.string(),
    displayName: z.string(),
    locale: z.string().nullish(),
  }),
  activeTenant: tenantReferenceSchema.nullish(),
  memberships: z.array(membershipSchema),
})
export type AuthResult = z.infer<typeof authResultSchema>

/** Response of `POST /auth/tenants/select`. */
export const tenantSelectionSchema = z.object({
  accessToken: z.string(),
  expiresIn: z.number(),
  activeTenant: tenantReferenceSchema,
})
export type TenantSelection = z.infer<typeof tenantSelectionSchema>
