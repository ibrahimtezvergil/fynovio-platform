import type { Action, ActionContext } from './types'

const actions = new Map<string, Action>()

export function registerAction<Ctx extends ActionContext>(action: Action<Ctx>): void {
  actions.set(action.id, action as Action)
}

export function getActions<Ctx extends ActionContext>(ctx: Ctx): Action<Ctx>[] {
  return [...actions.values()].filter((action) => action.when(ctx)) as Action<Ctx>[]
}
