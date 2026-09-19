import type { LucideIcon } from 'lucide-react'
import type { NavigateFunction } from 'react-router-dom'
import type { ActionContext } from '@/lib/actions'
import type { PermissionId } from '@/lib/permissions'
import type { NavGroup } from '@/types'

export interface CommandContext {
  navigate: NavigateFunction
  navGroups: readonly NavGroup[]
  actionContext: ActionContext
  actionGroup: string
}

/**
 * `permission` makes a command inspectable and driveable without touching the
 * DOM — the contract Claude Task F (#60, AI-Ready UI Metadata) exists for. See
 * the matching note on `ActionContext` in `@/lib/actions`.
 */
export interface Command<Ctx extends CommandContext = CommandContext> {
  id: string
  label: string
  group: string
  icon?: LucideIcon
  /** The permission this command is gated on, if any — see `usePermission` in `@/lib/permissions`. */
  permission?: PermissionId
  when: (ctx: Ctx) => boolean
  run: (ctx: Ctx) => void | Promise<void>
}
