import type { LucideIcon } from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { Card } from '@/components/ui/card'

export interface PageNavItem {
  to: string
  label: string
  icon: LucideIcon
}

/** Navigation for page-sized sections. It changes the route instead of scrolling to an anchor. */
export function PageNav({ items, label }: { items: readonly PageNavItem[]; label: string }) {
  return (
    <Card className="gap-0 px-2 py-2.5">
      <nav aria-label={label} className="flex flex-col gap-0.5">
        {items.map(({ to, label: itemLabel, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            end
            className={({ isActive }) => `nx-nav-item ${isActive ? 'bg-[var(--nx-fill-hover)] font-[590]' : ''}`}
          >
            <Icon aria-hidden className="size-[18px] shrink-0" strokeWidth={1.7} />
            <span className="flex-1">{itemLabel}</span>
          </NavLink>
        ))}
      </nav>
    </Card>
  )
}
