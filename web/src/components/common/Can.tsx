import type { ReactNode } from 'react'
import { usePermission, type PermissionId } from '@/lib/permissions'

interface CanProps {
  action: PermissionId
  children: ReactNode
  fallback?: ReactNode
}

/** Renders its children only when the current client-side UX policy permits it. */
export function Can({ action, children, fallback = null }: CanProps) {
  return usePermission(action) ? children : fallback
}
