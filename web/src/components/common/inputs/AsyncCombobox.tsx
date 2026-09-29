import { Combobox } from '@base-ui/react/combobox'
import { Check, ChevronDown, Loader2, X } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL, POPUP, POPUP_EMPTY, POPUP_ITEM } from './styles'
import type { FieldControlProps, SelectOption } from './types'

const TAIL_BUTTON = [
  'flex w-8 shrink-0 cursor-pointer items-center justify-center border-0 bg-transparent p-0',
  'text-muted-foreground outline-none transition-colors duration-[150ms] ease-fluid hover:text-foreground',
].join(' ')

type Status = 'idle' | 'loading' | 'ready' | 'error'

export interface AsyncComboboxProps<T extends string> extends FieldControlProps {
  /**
   * The whole option, not just its id: a server-backed field cannot look a
   * label back up from a local list, so the selection has to carry its own.
   */
  value: SelectOption<T> | null
  onValueChange: (value: SelectOption<T> | null) => void
  /** Aborted and re-issued as the query settles; resolve with the page of hits. */
  onSearch: (query: string, signal: AbortSignal) => Promise<SelectOption<T>[]>
  minQueryLength?: number
  debounceMs?: number
  placeholder?: string
  emptyMessage?: string
  errorMessage?: string
  disabled?: boolean
  className?: string
  /**
   * Pinned under the results (and under "no results"), for the one action that belongs to this search — "create what I
   * just typed". `close` dismisses the list; the footer runs its own action.
   */
  footer?: (context: { query: string; close: () => void }) => ReactNode
}

/**
 * A combobox whose options live on the server.
 *
 * Client-side filtering stops working somewhere around a few hundred rows, and
 * a customer list is not a few hundred rows. Every keystroke past the minimum
 * length settles for `debounceMs`, then issues one request whose predecessor is
 * aborted — so a slow early response can never overwrite a fast later one.
 */
export function AsyncCombobox<T extends string>({
  value,
  onValueChange,
  onSearch,
  minQueryLength = 2,
  debounceMs = 300,
  placeholder,
  emptyMessage,
  errorMessage,
  disabled,
  className,
  footer,
  id,
  ...aria
}: AsyncComboboxProps<T>) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('asyncCombobox.placeholder', { count: minQueryLength })
  const resolvedEmptyMessage = emptyMessage ?? t('asyncCombobox.empty')
  const resolvedErrorMessage = errorMessage ?? t('asyncCombobox.error')
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SelectOption<T>[]>([])
  const [fetchStatus, setFetchStatus] = useState<Status>('idle')
  const [open, setOpen] = useState(false)

  const trimmed = query.trim()
  // Selecting an item rewrites the input to its label; that is not a search.
  const searchable = trimmed.length >= minQueryLength && trimmed !== value?.label
  // Derived rather than written back from the effect: below the minimum length
  // there is nothing to synchronise, only a state the render already knows.
  const status: Status = searchable ? fetchStatus : 'idle'
  const visible = searchable ? results : []

  // Held in a ref so an inline `onSearch` can't restart the request on every
  // render — the effect is keyed by the query, which is what actually changed.
  const searchRef = useRef(onSearch)
  useEffect(() => {
    searchRef.current = onSearch
  })

  useEffect(() => {
    if (!searchable) return

    const controller = new AbortController()
    // Loading is entered when the debounce elapses, not when the effect runs:
    // a keystroke that is still being typed is not yet a request in flight.
    const timer = setTimeout(() => {
      setFetchStatus('loading')
      searchRef
        .current(trimmed, controller.signal)
        .then((options) => {
          if (controller.signal.aborted) return
          setResults(options)
          setFetchStatus('ready')
        })
        .catch(() => {
          if (!controller.signal.aborted) setFetchStatus('error')
        })
    }, debounceMs)

    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [trimmed, searchable, debounceMs])

  return (
    <Combobox.Root
      items={visible}
      value={value}
      onValueChange={(item: SelectOption<T> | null) => onValueChange(item)}
      onInputValueChange={setQuery}
      isItemEqualToValue={(a: SelectOption<T>, b: SelectOption<T>) => a.value === b.value}
      // The server already filtered; filtering the same list again locally would
      // hide hits the server matched on a field the label doesn't show.
      filter={null}
      disabled={disabled}
      open={open}
      onOpenChange={setOpen}
    >
      <Combobox.InputGroup className={cn(CONTROL_SHELL, className)}>
        <Combobox.Input
          id={id}
          placeholder={resolvedPlaceholder}
          className={CONTROL_INPUT}
          {...aria}
        />
        {status === 'loading' && (
          <span className="text-muted-foreground flex shrink-0 items-center px-1">
            <Loader2 aria-hidden className="size-4 animate-spin" strokeWidth={1.75} />
            <span className="sr-only">{t('asyncCombobox.searching')}</span>
          </span>
        )}
        {value && (
          <Combobox.Clear className={TAIL_BUTTON} aria-label={t('asyncCombobox.clearSelection')}>
            <X aria-hidden className="size-4" strokeWidth={1.75} />
          </Combobox.Clear>
        )}
        <Combobox.Trigger className={cn(TAIL_BUTTON, 'pr-2')} aria-label={t('asyncCombobox.openList')}>
          <ChevronDown aria-hidden className="size-4" strokeWidth={1.7} />
        </Combobox.Trigger>
      </Combobox.InputGroup>

      <Combobox.Portal>
        <Combobox.Positioner sideOffset={6} className="z-50 outline-none">
          <Combobox.Popup className={cn(POPUP, 'w-[var(--anchor-width)]')}>
            {status !== 'ready' && (
              <p className={POPUP_EMPTY}>
                {status === 'loading'
                  ? t('asyncCombobox.searchingEllipsis')
                  : status === 'error'
                    ? resolvedErrorMessage
                    : t('asyncCombobox.minLength', { count: minQueryLength })}
              </p>
            )}
            {status === 'ready' && visible.length === 0 && (
              <p className={POPUP_EMPTY}>{resolvedEmptyMessage}</p>
            )}
            <Combobox.List className="max-h-[min(20rem,var(--available-height))] overflow-y-auto overscroll-contain outline-none">
              {(option: SelectOption<T>) => (
                <Combobox.Item key={option.value} value={option} className={POPUP_ITEM}>
                  <Combobox.ItemIndicator className="col-start-1 flex">
                    <Check aria-hidden className="size-4" strokeWidth={2.25} />
                  </Combobox.ItemIndicator>
                  <span className="col-start-2 flex min-w-0 flex-col">
                    <span className="truncate">{option.label}</span>
                    {option.description && (
                      <span className="text-muted-foreground truncate text-[11.5px]">
                        {option.description}
                      </span>
                    )}
                  </span>
                </Combobox.Item>
              )}
            </Combobox.List>
            {footer && (
              // A mouse-down on the footer must not move focus out of the input, or the list would close before the click lands.
              <div className="border-t" onMouseDown={(event) => event.preventDefault()}>
                {footer({ query: trimmed === value?.label ? '' : trimmed, close: () => setOpen(false) })}
              </div>
            )}
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  )
}
