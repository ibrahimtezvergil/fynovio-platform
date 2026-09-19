import { ChevronRight } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, useLocation } from 'react-router-dom'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { buildSidebarNav, isParentActive } from '@/layouts/navigation'
import type { NavScope } from '@/lib/navigation/types'
import { cn } from '@/lib/utils'
import { isNavParent, type NavItem, type NavLeaf, type NavParent } from '@/types'

/** Hover-intent for the rail flyouts: long enough not to fire on a pass-by. */
const FLYOUT_DELAY = 220
const FLYOUT_CLOSE_DELAY = 140

export function SidebarNav({ collapsed, scope }: { collapsed: boolean; scope: NavScope }) {
  const { t } = useTranslation('nav')
  const { pathname } = useLocation()
  const navGroups = useMemo(() => buildSidebarNav(t, scope), [t, scope])

  return (
    <div
      className={cn(
        '-mx-0.5 flex min-h-0 flex-1 flex-col gap-[18px] overflow-y-auto overflow-x-hidden px-0.5',
        // The rail has no group headings, so the gaps do the separating.
        collapsed && 'nx-rail-scroll items-center gap-3.5',
      )}
    >
      {navGroups.map((group) => (
        <nav
          key={group.label}
          aria-label={group.label}
          className={cn('flex flex-col gap-0.5', collapsed && 'items-center')}
        >
          {!collapsed && <span className="nx-nav-heading">{group.label}</span>}
          {group.items.map((item) =>
            collapsed ? (
              <RailItem key={item.id} item={item} pathname={pathname} />
            ) : (
              <ExpandedItem key={item.id} item={item} pathname={pathname} />
            ),
          )}
        </nav>
      ))}
    </div>
  )
}

/* ---- expanded ------------------------------------------------------------ */

function ExpandedItem({ item, pathname }: { item: NavItem; pathname: string }) {
  if (isNavParent(item)) return <ExpandedParent item={item} pathname={pathname} />

  return (
    <NavLink to={item.to} className="nx-nav-item">
      {({ isActive }) => (
        <>
          <item.icon aria-hidden className="size-[18px] shrink-0" strokeWidth={1.7} />
          <span className="flex-1 truncate">{item.label}</span>
          <Badge value={item.badge} isActive={isActive} />
        </>
      )}
    </NavLink>
  )
}

function ExpandedParent({ item, pathname }: { item: NavParent; pathname: string }) {
  const active = isParentActive(item, pathname)
  const [open, setOpen] = useState(active)
  const [wasActive, setWasActive] = useState(active)

  // Landing on a sub-page — from a link, the flyout, or a reload — reveals the
  // section that owns it; closing it again stays the user's call. Adjusted
  // during render rather than in an effect, so the disclosure never paints
  // shut for a frame first.
  if (active !== wasActive) {
    setWasActive(active)
    if (active) setOpen(true)
  }

  return (
    <>
      <button
        type="button"
        aria-expanded={open}
        // Active, but not `aria-current` — the open page is a child, not this row.
        data-active={active || undefined}
        onClick={() => setOpen((value) => !value)}
        className="nx-nav-item w-full cursor-pointer text-left"
      >
        <item.icon aria-hidden className="size-[18px] shrink-0" strokeWidth={1.7} />
        <span className="flex-1 truncate">{item.label}</span>
        <ChevronRight
          aria-hidden
          className={cn(
            'size-3.5 shrink-0 opacity-60 transition-transform duration-[250ms] ease-fluid',
            open && 'rotate-90',
          )}
        />
      </button>

      {/* 0fr → 1fr animates a height the content measures itself. */}
      <div
        className={cn(
          'grid transition-[grid-template-rows] duration-[250ms] ease-fluid',
          open ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]',
        )}
      >
        <div className="overflow-hidden">
          <div className="flex flex-col gap-0.5 pt-0.5">
            {item.children.map((leaf) => (
              <NavLink
                key={leaf.id}
                to={leaf.to}
                tabIndex={open ? undefined : -1}
                className="nx-nav-item nx-nav-item--sub"
              >
                {({ isActive }) => (
                  <>
                    <span className="flex-1 truncate">{leaf.label}</span>
                    <Badge value={leaf.badge} isActive={isActive} />
                  </>
                )}
              </NavLink>
            ))}
          </div>
        </div>
      </div>
    </>
  )
}

/* ---- collapsed rail ------------------------------------------------------ */

function RailItem({ item, pathname }: { item: NavItem; pathname: string }) {
  if (isNavParent(item)) return <RailParent item={item} pathname={pathname} />

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <NavLink to={item.to} aria-label={item.label} className="nx-nav-item nx-nav-item--rail" />
        }
      >
        <item.icon aria-hidden className="size-[19px] shrink-0" strokeWidth={1.7} />
        <RailBadgeDot value={item.badge} />
      </TooltipTrigger>
      <TooltipContent side="right" sideOffset={10}>
        {item.label}
        {item.badge != null && <span className="tnum opacity-70">{item.badge}</span>}
      </TooltipContent>
    </Tooltip>
  )
}

function RailParent({ item, pathname }: { item: NavParent; pathname: string }) {
  const active = isParentActive(item, pathname)
  const [open, setOpen] = useState(false)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        openOnHover
        delay={FLYOUT_DELAY}
        closeDelay={FLYOUT_CLOSE_DELAY}
        render={
          <button
            type="button"
            aria-label={item.label}
            data-active={active || undefined}
            className="nx-nav-item nx-nav-item--rail cursor-pointer"
          />
        }
      >
        <item.icon aria-hidden className="size-[19px] shrink-0" strokeWidth={1.7} />
        <RailBadgeDot value={item.badge} />
      </PopoverTrigger>

      <PopoverContent
        side="right"
        align="start"
        sideOffset={10}
        alignOffset={-8}
        className="nx-flyout w-auto gap-0.5 rounded-[var(--nx-r-ctl-lg)] p-2"
      >
        <span className="nx-flyout__title">{item.label}</span>
        {item.children.map((leaf) => (
          <FlyoutLink key={leaf.id} leaf={leaf} onNavigate={() => setOpen(false)} />
        ))}
      </PopoverContent>
    </Popover>
  )
}

function FlyoutLink({ leaf, onNavigate }: { leaf: NavLeaf; onNavigate: () => void }) {
  return (
    <NavLink to={leaf.to} onClick={onNavigate} className="nx-nav-item px-3">
      {({ isActive }) => (
        <>
          {leaf.icon && (
            <leaf.icon aria-hidden className="size-[17px] shrink-0" strokeWidth={1.7} />
          )}
          <span className="flex-1 truncate pr-4">{leaf.label}</span>
          <Badge value={leaf.badge} isActive={isActive} />
        </>
      )}
    </NavLink>
  )
}

/* ---- shared bits --------------------------------------------------------- */

function Badge({ value, isActive }: { value?: number; isActive: boolean }) {
  if (value == null) return null
  return (
    <span className={cn('tnum text-[11.5px]', isActive ? 'opacity-80' : 'text-muted-foreground')}>
      {value}
    </span>
  )
}

/** The rail has no room for the count, so the badge degrades to presence. */
function RailBadgeDot({ value }: { value?: number }) {
  if (value == null) return null
  return (
    <span
      aria-hidden
      className="bg-[var(--nx-tint)] absolute top-[9px] right-[9px] size-[5px] rounded-full"
    />
  )
}
