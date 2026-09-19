import { Search, X } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { useDebouncedValue } from '@/components/data-table/hooks/useDebouncedValue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

interface DataTableToolbarProps {
  /** Committed value — normally the URL-owned global filter. */
  value: string
  onChange: (value: string) => void
  placeholder?: string
  debounceMs?: number
  /** Right-hand slot: view options, bulk actions, mode switches. */
  children?: ReactNode
  className?: string
}

/**
 * Search box for `globalFilteringFeature`.
 *
 * Keystrokes stay local; only the settled value reaches `onChange`, which in
 * these grids writes the URL and — in server mode — the query key. Without the
 * debounce, every keypress would be a history entry and a request.
 */
export function DataTableToolbar({
  value,
  onChange,
  placeholder = 'Search…',
  debounceMs = 250,
  children,
  className,
}: DataTableToolbarProps) {
  const [draft, setDraft] = useState(value)
  const [lastExternal, setLastExternal] = useState(value)
  const debounced = useDebouncedValue(draft, debounceMs)

  // Adjusting state during render, not in an effect: when the committed value
  // changes from outside (back button, reset, a shared link) the draft follows
  // it without a second render pass.
  if (value !== lastExternal) {
    setLastExternal(value)
    setDraft(value)
  }

  // Push settled input outwards. Once `onChange` lands, `value` catches up and
  // this becomes a no-op, so the two directions cannot chase each other.
  useEffect(() => {
    if (debounced !== value) onChange(debounced)
  }, [debounced, value, onChange])

  return (
    <div className={cn('flex flex-wrap items-center justify-between gap-3', className)}>
      <div className="relative min-w-[220px] flex-1 sm:max-w-xs">
        <Search
          aria-hidden
          className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2"
          strokeWidth={1.75}
        />
        <Input
          type="search"
          role="searchbox"
          aria-label={placeholder}
          placeholder={placeholder}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          className="pl-8"
        />
        {draft.length > 0 && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label="Clear search"
            onClick={() => setDraft('')}
            className="absolute top-1/2 right-1 -translate-y-1/2"
          >
            <X aria-hidden className="size-3" />
          </Button>
        )}
      </div>

      {children && <div className="flex shrink-0 flex-wrap items-center gap-2">{children}</div>}
    </div>
  )
}
