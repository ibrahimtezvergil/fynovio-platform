import { Building2, FileText, Package } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import type { RecentWorkItem, RecentWorkEntityType } from '@/features/home/schema'

const ENTITY_ICON: Record<RecentWorkEntityType, LucideIcon> = {
  quote: FileText,
  order: Package,
  customer: Building2,
}

/** One clickable line back to a recently touched object. Lower visual weight than Attention. */
export function RecentWorkRow({ item }: { item: RecentWorkItem }) {
  const navigate = useNavigate()
  const Icon = ENTITY_ICON[item.entityType]

  return (
    <button
      type="button"
      onClick={() => navigate(item.route)}
      className="hover:bg-[var(--nx-fill-hover)] flex w-full cursor-pointer items-center gap-3 rounded-[var(--nx-r-ctl)] px-2.5 py-2 text-left transition-colors duration-[200ms] ease-fluid"
    >
      <span aria-hidden className="nx-icon-tile size-8 shrink-0 rounded-[var(--nx-r-ctl-sm)]">
        <Icon className="size-4" strokeWidth={1.7} />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[13px] font-[590]">{item.title}</p>
        {item.subtitle && <p className="text-muted-foreground truncate text-[12px]">{item.subtitle}</p>}
      </div>
      <span className="text-muted-foreground shrink-0 text-[11.5px]">{item.lastAccessedAt}</span>
    </button>
  )
}
