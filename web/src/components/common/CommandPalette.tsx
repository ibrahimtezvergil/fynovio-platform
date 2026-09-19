import { Command as CommandPrimitive } from 'cmdk'
import { Building2, FileText, Package, Search } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { getCommands } from '@/lib/commands'
import type { Command, CommandContext } from '@/lib/commands'
import { MOCK_SEARCH_ENTITIES, type SearchResultItem, type SearchResultType } from '@/lib/search/entitySearch'
import { buildNavGroups } from '@/layouts/navigation'

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

function groupCommands(commands: readonly Command[]): Map<string, Command[]> {
  return commands.reduce((groups, command) => {
    const group = groups.get(command.group) ?? []
    group.push(command)
    groups.set(command.group, group)
    return groups
  }, new Map<string, Command[]>())
}

const ENTITY_ICON: Record<SearchResultType, LucideIcon> = {
  customer: Building2,
  quote: FileText,
  order: Package,
}

function groupEntities(entities: readonly SearchResultItem[]): Map<SearchResultType, SearchResultItem[]> {
  return entities.reduce((groups, entity) => {
    const group = groups.get(entity.type) ?? []
    group.push(entity)
    groups.set(entity.type, group)
    return groups
  }, new Map<SearchResultType, SearchResultItem[]>())
}

/** Global, keyboard-first access to registered routes and context-valid actions. */
export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const { t } = useTranslation('nav')
  const navigate = useNavigate()
  const navGroups = useMemo(() => buildNavGroups(t), [t])
  const context = useMemo<CommandContext>(
    () => ({ navigate, navGroups, actionContext: {}, actionGroup: t('commandPalette.actions') }),
    [navigate, navGroups, t],
  )
  const commands = useMemo(() => getCommands(context), [context])
  const grouped = useMemo(() => groupCommands(commands), [commands])
  const entityGroups = useMemo(() => groupEntities(MOCK_SEARCH_ENTITIES), [])
  const entityGroupLabels: Record<SearchResultType, string> = {
    customer: t('commandPalette.groups.customers'),
    quote: t('commandPalette.groups.quotes'),
    order: t('commandPalette.groups.orders'),
  }

  const select = (id: string) => {
    const command = commands.find((candidate) => candidate.id === id)
    if (!command) return
    onOpenChange(false)
    void command.run(context)
  }

  const selectEntity = (entity: SearchResultItem) => {
    onOpenChange(false)
    navigate(entity.url)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent showCloseButton={false} className="gap-0 overflow-hidden p-0 sm:max-w-xl">
        <DialogHeader className="sr-only">
          <DialogTitle>{t('commandPalette.title')}</DialogTitle>
          <DialogDescription>{t('commandPalette.description')}</DialogDescription>
        </DialogHeader>
        <CommandPrimitive
          label={t('commandPalette.title')}
          // Plain substring, not cmdk's default fuzzy scorer: a fuzzy match
          // lets an unrelated entity (e.g. "Baltic Freight AB") surface under
          // an unrelated query, which the brief explicitly rules out — search
          // categories must read as clean, literal matches.
          //
          // Locale-insensitive lowercasing on purpose: entity labels are
          // largely Latin-script company names ("ACME Holding"), and
          // `toLocaleLowerCase('tr')` folds an ASCII "I" to dotless "ı" —
          // typing "HOLDING" would then fail to match "Holding".
          filter={(value, search) => (value.toLowerCase().includes(search.toLowerCase()) ? 1 : 0)}
          className="flex max-h-[min(28rem,calc(100dvh-3rem))] flex-col"
        >
          <div className="flex items-center gap-3 border-b px-4">
            <Search aria-hidden className="text-muted-foreground size-4 shrink-0" strokeWidth={1.7} />
            <CommandPrimitive.Input
              placeholder={t('commandPalette.placeholder')}
              className="h-12 min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-muted-foreground"
            />
            <kbd aria-hidden className="nx-kbd">Esc</kbd>
          </div>
          <CommandPrimitive.List className="max-h-80 overflow-y-auto p-2">
            <CommandPrimitive.Empty className="px-3 py-8 text-center text-[13px] text-muted-foreground">
              {t('commandPalette.empty')}
            </CommandPrimitive.Empty>
            {[...grouped].map(([group, groupCommands]) => (
              <CommandPrimitive.Group key={group} heading={group} className="mb-2 last:mb-0 [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1.5 [&_[cmdk-group-heading]]:text-[11px] [&_[cmdk-group-heading]]:font-[600] [&_[cmdk-group-heading]]:uppercase [&_[cmdk-group-heading]]:tracking-[0.06em] [&_[cmdk-group-heading]]:text-muted-foreground">
                {groupCommands?.map((command) => {
                  const Icon = command.icon
                  return (
                    <CommandPrimitive.Item
                      key={command.id}
                      value={command.label}
                      onSelect={() => select(command.id)}
                      className="flex cursor-default items-center gap-3 rounded-md px-2.5 py-2 text-[13.5px] outline-none data-[selected=true]:bg-muted"
                    >
                      {Icon && <Icon aria-hidden className="text-muted-foreground size-4 shrink-0" strokeWidth={1.7} />}
                      <span>{command.label}</span>
                    </CommandPrimitive.Item>
                  )
                })}
              </CommandPrimitive.Group>
            ))}
            {[...entityGroups].map(([type, entities]) => (
              <CommandPrimitive.Group key={type} heading={entityGroupLabels[type]} className="mb-2 last:mb-0 [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1.5 [&_[cmdk-group-heading]]:text-[11px] [&_[cmdk-group-heading]]:font-[600] [&_[cmdk-group-heading]]:uppercase [&_[cmdk-group-heading]]:tracking-[0.06em] [&_[cmdk-group-heading]]:text-muted-foreground">
                {entities.map((entity) => {
                  const Icon = ENTITY_ICON[entity.type]
                  return (
                    <CommandPrimitive.Item
                      key={entity.id}
                      value={`${entity.label} ${entity.subtitle}`}
                      onSelect={() => selectEntity(entity)}
                      className="flex cursor-default items-center gap-3 rounded-md px-2.5 py-2 text-[13.5px] outline-none data-[selected=true]:bg-muted"
                    >
                      <Icon aria-hidden className="text-muted-foreground size-4 shrink-0" strokeWidth={1.7} />
                      <span className="min-w-0 flex-1 truncate">{entity.label}</span>
                      <span className="text-muted-foreground shrink-0 text-[12px]">{entity.subtitle}</span>
                    </CommandPrimitive.Item>
                  )
                })}
              </CommandPrimitive.Group>
            ))}
          </CommandPrimitive.List>
        </CommandPrimitive>
      </DialogContent>
    </Dialog>
  )
}
