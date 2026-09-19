import { useNavigate } from 'react-router-dom'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import type { TeamActivityItem } from '@/features/home/schema'

/** Calm presence, not an activity log — one line per teammate, no timeline chrome. */
export function TeamActivityRow({ item }: { item: TeamActivityItem }) {
  const navigate = useNavigate()

  return (
    <button
      type="button"
      disabled={!item.route}
      onClick={() => item.route && navigate(item.route)}
      className="hover:enabled:bg-[var(--nx-fill-hover)] flex w-full items-center gap-3 rounded-[var(--nx-r-ctl)] px-2.5 py-2 text-left transition-colors duration-[200ms] ease-fluid disabled:cursor-default"
    >
      <Avatar size="sm" className="shrink-0">
        <AvatarFallback>{item.avatarInitials}</AvatarFallback>
      </Avatar>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[13px]">
          <span className="font-[590]">{item.user}</span> <span className="text-muted-foreground">{item.action}</span>
        </p>
      </div>
      <span className="text-muted-foreground shrink-0 text-[11.5px]">{item.timestamp}</span>
    </button>
  )
}
