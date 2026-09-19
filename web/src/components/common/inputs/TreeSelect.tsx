import { Popover } from '@base-ui/react/popover'
import { Check, ChevronDown, ChevronRight, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL, POPUP } from './styles'
import type { FieldControlProps } from './types'

export interface TreeNode {
  value: string
  label: string
  /** Shown before the label — an account code, an SKU prefix. */
  code?: string
  children?: TreeNode[]
  disabled?: boolean
}

export interface TreeSelectProps extends FieldControlProps {
  nodes: readonly TreeNode[]
  value: string | null
  onValueChange: (value: string | null) => void
  /** Off by default: a chart of accounts is posted to at its leaves. */
  selectableBranches?: boolean
  placeholder?: string
  searchPlaceholder?: string
  disabled?: boolean
  className?: string
}

/** The chain of labels down to `value` — what the trigger shows. */
function pathTo(nodes: readonly TreeNode[], value: string): TreeNode[] | null {
  for (const node of nodes) {
    if (node.value === value) return [node]
    const below = node.children && pathTo(node.children, value)
    if (below) return [node, ...below]
  }
  return null
}

/** Every branch that contains a match, so a hit is never hidden under a fold. */
function branchesMatching(nodes: readonly TreeNode[], query: string): Set<string> {
  const open = new Set<string>()
  const walk = (node: TreeNode): boolean => {
    const hit = matches(node, query)
    const childHit = (node.children ?? []).map(walk).some(Boolean)
    if (childHit) open.add(node.value)
    return hit || childHit
  }
  nodes.forEach(walk)
  return open
}

const matches = (node: TreeNode, query: string) =>
  `${node.code ?? ''} ${node.label}`.toLocaleLowerCase('tr').includes(query.toLocaleLowerCase('tr'))

/** Keeps a node when it matches, or when anything under it does. */
function visible(node: TreeNode, query: string): boolean {
  if (query.length === 0) return true
  return matches(node, query) || (node.children ?? []).some((child) => visible(child, query))
}

/**
 * A hierarchy picked at its own shape: chart of accounts, product families,
 * organisational units.
 *
 * Flattening a tree into a select loses the one thing the user navigates by —
 * where a code sits under its parent — and forces every label to repeat its
 * whole path to stay unambiguous.
 */
export function TreeSelect({
  nodes,
  value,
  onValueChange,
  selectableBranches = false,
  placeholder,
  searchPlaceholder,
  disabled,
  className,
  id,
  ...aria
}: TreeSelectProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('treeSelect.placeholder')
  const resolvedSearchPlaceholder = searchPlaceholder ?? t('treeSelect.searchPlaceholder')
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const path = value ? pathTo(nodes, value) : null
  const autoExpanded = useMemo(
    () => (query.length > 0 ? branchesMatching(nodes, query) : null),
    [nodes, query],
  )
  const isOpen = (node: TreeNode) => autoExpanded?.has(node.value) ?? expanded.has(node.value)

  const toggle = (node: TreeNode) =>
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(node.value)) next.delete(node.value)
      else next.add(node.value)
      return next
    })

  const renderNodes = (list: readonly TreeNode[], depth: number) =>
    list
      .filter((node) => visible(node, query))
      .map((node) => {
        const branch = (node.children?.length ?? 0) > 0
        const selectable = !node.disabled && (!branch || selectableBranches)
        const selected = node.value === value
        return (
          <li key={node.value}>
            <div
              className={cn(
                'flex items-center gap-1 rounded-sm',
                selected && 'bg-accent text-accent-foreground',
              )}
              style={{ paddingInlineStart: depth * 14 }}
            >
              {branch ? (
                <button
                  type="button"
                  aria-label={
                    isOpen(node)
                      ? t('treeSelect.collapse', { label: node.label })
                      : t('treeSelect.expand', { label: node.label })
                  }
                  aria-expanded={isOpen(node)}
                  onClick={() => toggle(node)}
                  className="text-muted-foreground hover:text-foreground flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent outline-none"
                >
                  {isOpen(node) ? (
                    <ChevronDown aria-hidden className="size-3.5" strokeWidth={2} />
                  ) : (
                    <ChevronRight aria-hidden className="size-3.5" strokeWidth={2} />
                  )}
                </button>
              ) : (
                <span aria-hidden className="size-6 shrink-0" />
              )}
              <button
                type="button"
                disabled={!selectable}
                aria-pressed={selected}
                onClick={() => {
                  onValueChange(node.value)
                  setOpen(false)
                }}
                className={cn(
                  'flex min-w-0 flex-1 cursor-pointer items-center gap-2 rounded-sm border-0 bg-transparent px-1.5 py-1.5 text-left outline-none',
                  'text-[13px] transition-colors duration-[120ms] ease-fluid hover:bg-[var(--nx-fill-hover)]',
                  !selectable && 'cursor-default text-muted-foreground hover:bg-transparent',
                )}
              >
                {node.code && (
                  <span className="tnum text-muted-foreground shrink-0 text-[11.5px]">
                    {node.code}
                  </span>
                )}
                <span className="truncate">{node.label}</span>
                {selected && (
                  <Check aria-hidden className="ml-auto size-3.5 shrink-0" strokeWidth={2.5} />
                )}
              </button>
            </div>
            {branch && isOpen(node) && (
              <ul>{renderNodes(node.children ?? [], depth + 1)}</ul>
            )}
          </li>
        )
      })

  return (
    <Popover.Root open={open} onOpenChange={setOpen}>
      <Popover.Trigger
        id={id}
        disabled={disabled}
        className={cn(
          CONTROL_SHELL,
          'cursor-pointer items-center justify-between gap-2 px-[13px] text-left text-[13.5px] outline-none',
          'data-popup-open:border-ring data-popup-open:bg-[var(--nx-surface)]',
          'disabled:pointer-events-none disabled:opacity-45',
          className,
        )}
        {...aria}
      >
        <span className={cn('truncate', !path && 'text-[var(--nx-label-3)]')}>
          {path ? path.map((node) => node.code ?? node.label).join(' › ') : resolvedPlaceholder}
        </span>
        <ChevronDown
          aria-hidden
          className="text-muted-foreground size-4 shrink-0"
          strokeWidth={1.7}
        />
      </Popover.Trigger>

      <Popover.Portal>
        <Popover.Positioner sideOffset={6} align="start" className="z-50 outline-none">
          <Popover.Popup
            className={cn(POPUP, 'flex w-[var(--anchor-width)] min-w-72 flex-col gap-1.5 p-2')}
          >
            <div className="flex h-9 items-center gap-2 rounded-sm border border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-2.5">
              <Search aria-hidden className="text-muted-foreground size-4" strokeWidth={1.75} />
              <input
                type="search"
                aria-label={t('treeSelect.searchAriaLabel')}
                value={query}
                placeholder={resolvedSearchPlaceholder}
                onChange={(event) => setQuery(event.target.value)}
                className={cn(CONTROL_INPUT, 'h-full px-0 text-[13px]')}
              />
            </div>
            <ul className="max-h-[min(20rem,var(--available-height))] overflow-y-auto overscroll-contain">
              {renderNodes(nodes, 0)}
            </ul>
          </Popover.Popup>
        </Popover.Positioner>
      </Popover.Portal>
    </Popover.Root>
  )
}
