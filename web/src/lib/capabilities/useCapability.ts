import { createContext, createElement, useContext, useMemo, type ReactNode } from 'react'

/** A stable tenant/package entitlement identifier. */
export type CapabilityId = string

/**
 * UX ONLY — this controls what the client renders, never authorization. A
 * backend must enforce every entitlement independently for every request.
 */
export const mockTenantCapabilities = ['pipeline.approval'] as const satisfies readonly CapabilityId[]

const CapabilityContext = createContext<ReadonlySet<CapabilityId>>(
  new Set(mockTenantCapabilities),
)

interface CapabilityProviderProps {
  children: ReactNode
  /** Local stand-in for the tenant package/entitlements returned by a backend. */
  capabilities?: readonly CapabilityId[]
}

/**
 * Overrides the mocked tenant package for a subtree. This is intentionally
 * small so a future tenant/entitlement resolver can own the provider's input.
 */
export function CapabilityProvider({
  children,
  capabilities = mockTenantCapabilities,
}: CapabilityProviderProps) {
  const value = useMemo(() => new Set(capabilities), [capabilities])
  return createElement(CapabilityContext.Provider, { value }, children)
}

/** Reactive UX predicate for an entitlement supplied by the nearest provider. */
export function useCapability(capability: CapabilityId): boolean {
  return useContext(CapabilityContext).has(capability)
}
