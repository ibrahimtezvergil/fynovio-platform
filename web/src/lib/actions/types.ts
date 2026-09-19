import type { LucideIcon } from 'lucide-react'
import type { PermissionId } from '@/lib/permissions'
import type { EntityRef } from '@/types/entity'

/**
 * `entity` and `permission` make an action's context inspectable and driveable
 * without touching the DOM — the contract Claude Task F (#60, AI-Ready UI
 * Metadata) exists for. An agent (or a future palette/audit layer) reads
 * `entity` to know what a run affects and `permission` to know whether it can,
 * without parsing `run`'s implementation.
 */
export interface ActionContext {
  entity?: EntityRef
  /** The permission gating this context's action, if any — see `usePermission` in `@/lib/permissions`. */
  permission?: PermissionId
  [key: string]: unknown
}

export interface Action<Ctx extends ActionContext = ActionContext> {
  id: string
  label: string
  icon?: LucideIcon
  when: (ctx: Ctx) => boolean
  run: (ctx: Ctx) => void | Promise<void>
  undo?: (ctx: Ctx) => void | Promise<void>
}
