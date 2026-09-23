import { ChevronsUpDown } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useSessionStore } from '@/lib/auth'
import { cn } from '@/lib/utils'
import { TenantSwitcher } from './TenantSwitcher'

/**
 * The sidebar's active-organisation slot — square avatar: an org, not a person.
 * Accounts with several memberships open the switcher from it; single-tenant
 * accounts just see which organisation they are in. Memberships carry the
 * display name used for the label.
 */
export function TenantMenu({ collapsed }: { collapsed: boolean }) {
  const activeTenantId = useSessionStore((s) => s.activeTenantId)
  const memberships = useSessionStore((s) => s.memberships)
  const switchable = useSessionStore((s) => s.memberships.length > 1)
  const [open, setOpen] = useState(false)

  if (activeTenantId === null) return null

  const label = memberships.find((membership) => membership.tenantId === activeTenantId)?.displayName
  const slotClass = cn(
    'nx-material flex h-11 items-center gap-2.5 rounded-[16px] text-left text-[13.5px] font-medium',
    collapsed ? 'w-11 shrink-0 justify-center px-0' : 'w-full px-[11px]',
  )
  const content: ReactNode = (
    <>
      <span aria-hidden className="nx-avatar size-[26px] shrink-0 rounded-[var(--nx-r-tile)] text-[11px]">
        {activeTenantId}
      </span>
      {!collapsed && (
        <>
          <span className="min-w-0 flex-1 truncate">{label}</span>
          {switchable && <ChevronsUpDown aria-hidden className="text-muted-foreground size-[15px] shrink-0" />}
        </>
      )}
    </>
  )

  if (!switchable) {
    return (
      <div title={collapsed ? label : undefined} className={slotClass}>
        {content}
        {collapsed && <span className="sr-only">{label}</span>}
      </div>
    )
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <Tooltip>
        <TooltipTrigger
          disabled={!collapsed || open}
          render={
            <PopoverTrigger
              render={
                <button
                  type="button"
                  aria-label={label}
                  className={cn(
                    slotClass,
                    'cursor-pointer transition-[background,border-color] duration-[250ms] ease-fluid',
                    'hover:bg-[var(--nx-fill-hover)] hover:border-[var(--nx-hairline-strong)]',
                  )}
                />
              }
            />
          }
        >
          {content}
        </TooltipTrigger>
        <TooltipContent side="right" sideOffset={10}>
          {label}
        </TooltipContent>
      </Tooltip>

      <PopoverContent
        side={collapsed ? 'right' : 'bottom'}
        align="start"
        sideOffset={collapsed ? 10 : 8}
        className={cn('gap-0 rounded-[var(--nx-r-card)] p-0', collapsed ? 'w-60' : 'w-(--anchor-width)')}
      >
        <TenantSwitcher onSwitched={() => setOpen(false)} />
      </PopoverContent>
    </Popover>
  )
}
