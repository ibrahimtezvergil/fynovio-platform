import { getActions } from '@/lib/actions'
import { isNavParent } from '@/types'
import type { Command, CommandContext } from './types'

const commands = new Map<string, Command>()

export function registerCommand<Ctx extends CommandContext>(command: Command<Ctx>): void {
  commands.set(command.id, command as Command)
}

/** Resolve registered, navigation and context-valid action commands in one place. */
export function getCommands<Ctx extends CommandContext>(ctx: Ctx): Command<Ctx>[] {
  const navigation = ctx.navGroups.flatMap((group) =>
    group.items.flatMap((item) => {
      const routes = isNavParent(item) ? item.children : [item]
      return routes.map<Command<Ctx>>((route) => ({
        id: `navigate.${route.id}`,
        label: route.label,
        group: group.label,
        icon: route.icon ?? item.icon,
        when: () => true,
        run: ({ navigate }) => navigate(route.to),
      }))
    }),
  )
  const actions = getActions(ctx.actionContext).map<Command<Ctx>>((action) => ({
    id: `action.${action.id}`,
    label: action.label,
    group: ctx.actionGroup,
    icon: action.icon,
    when: () => true,
    run: () => action.run(ctx.actionContext),
  }))

  return [...navigation, ...actions, ...commands.values()].filter((command) => command.when(ctx)) as Command<Ctx>[]
}
