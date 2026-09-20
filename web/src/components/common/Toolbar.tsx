import { Search, X } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { DensityScope } from '@/components/common/DensityScope'
import { useDebouncedValue } from '@/components/data-table'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

interface ToolbarProps {
  children: ReactNode
  className?: string
}

/**
 * The strip above a grid: search and filters left, view controls right.
 *
 * It is a density region, so the controls it holds shrink with the switch
 * while the page header above it — which is not one — does not. Composition
 * rather than slots: a toolbar's right half differs per page, and a `filters`
 * prop would only be a `children` with extra steps.
 *
 * ```tsx
 * <Toolbar>
 *   <ToolbarSearch value={query} onChange={setQuery} />
 *   <Button variant="secondary">Filtre</Button>
 *   <ToolbarSpacer />
 *   <DensityToggle />
 * </Toolbar>
 * ```
 */
export function Toolbar({ children, className }: ToolbarProps) {
  return (
    <DensityScope
      className={cn(
        'flex flex-wrap items-center gap-[var(--nx-d-gap)]',
        // Gap and control height both move on the switch — transition the
        // strip rather than let it jump. No `role="toolbar"`: that role owes
        // the user arrow-key roving focus, and a tab stop per control is the
        // honest behaviour for a strip holding a search box and menus.
        'transition-[gap] duration-[250ms] ease-fluid',
        className,
      )}
    >
      {children}
    </DensityScope>
  )
}

/** Pushes everything after it to the right edge. */
export function ToolbarSpacer() {
  return <span aria-hidden className="flex-1" />
}

/** Keeps related controls together at the tighter of the two gaps. */
export function ToolbarGroup({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn('flex flex-wrap items-center gap-[var(--nx-d-gap-sm)]', className)}>
      {children}
    </div>
  )
}

interface ToolbarSearchProps {
  /** Committed value — normally the store- or URL-owned query. */
  value: string
  onChange: (value: string) => void
  placeholder?: string
  debounceMs?: number
  className?: string
}

/**
 * Debounced search box.
 *
 * Keystrokes stay local; only the settled value reaches `onChange`, which
 * downstream is a filter pass today and a query key once there is a backend.
 * Height follows the density region it sits in, via `Input`'s `h-control`.
 */
export function ToolbarSearch({
  value,
  onChange,
  placeholder,
  debounceMs = 250,
  className,
}: ToolbarSearchProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('toolbarSearch.defaultPlaceholder')
  const [draft, setDraft] = useState(value)
  const [lastExternal, setLastExternal] = useState(value)
  const debounced = useDebouncedValue(draft, debounceMs)

  // Adjusting state during render, not in an effect: when the committed value
  // changes from outside (a reset, a shared link) the draft follows it without
  // a second render pass.
  if (value !== lastExternal) {
    setLastExternal(value)
    setDraft(value)
  }

  // Push settled input outwards. Once `onChange` lands, `value` catches up and
  // this becomes a no-op, so the two directions cannot chase each other.
  // `draft === debounced` is the "settled" test: right after an outside reset the
  // debounced copy still holds the old text, and pushing it would undo the reset.
  useEffect(() => {
    if (debounced !== value && draft === debounced) onChange(debounced)
  }, [debounced, draft, value, onChange])

  return (
    <div className={cn('relative min-w-[200px] flex-1 sm:max-w-[280px] sm:flex-none', className)}>
      <Search
        aria-hidden
        className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-3.5 -translate-y-1/2"
        strokeWidth={1.75}
      />
      <Input
        type="search"
        role="searchbox"
        aria-label={resolvedPlaceholder}
        placeholder={resolvedPlaceholder}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        className="pl-9"
      />
      {draft.length > 0 && (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={t('toolbarSearch.clear')}
          onClick={() => setDraft('')}
          className="absolute top-1/2 right-1.5 -translate-y-1/2"
        >
          <X aria-hidden className="size-3" />
        </Button>
      )}
    </div>
  )
}
