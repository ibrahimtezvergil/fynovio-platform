import { Loader2, Search, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

export interface SearchInputProps extends FieldControlProps {
  value: string
  onValueChange: (value: string) => void
  placeholder?: string
  /** Spinner in place of the clear button while a query is in flight. */
  loading?: boolean
  disabled?: boolean
  className?: string
}

/** Leading glass, trailing clear — the filter field every list view carries. */
export function SearchInput({
  value,
  onValueChange,
  placeholder,
  loading,
  disabled,
  className,
  id,
  ...aria
}: SearchInputProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('searchInput.placeholder')
  return (
    <div className={cn(CONTROL_SHELL, className)}>
      <span className="text-muted-foreground flex shrink-0 items-center pl-3">
        <Search aria-hidden className="size-4" strokeWidth={1.75} />
      </span>
      <input
        id={id}
        type="search"
        value={value}
        placeholder={resolvedPlaceholder}
        disabled={disabled}
        onChange={(event) => onValueChange(event.target.value)}
        className={cn(CONTROL_INPUT, 'pl-2.5 [&::-webkit-search-cancel-button]:hidden')}
        {...aria}
      />
      {loading ? (
        <span className="text-muted-foreground flex shrink-0 items-center pr-3">
          <Loader2 aria-hidden className="size-4 animate-spin" strokeWidth={1.75} />
        </span>
      ) : (
        value.length > 0 && (
          <button
            type="button"
            aria-label={t('searchInput.clear')}
            onClick={() => onValueChange('')}
            className="text-muted-foreground hover:text-foreground flex shrink-0 cursor-pointer items-center border-0 bg-transparent px-3 outline-none"
          >
            <X aria-hidden className="size-4" strokeWidth={1.75} />
          </button>
        )
      )}
    </div>
  )
}
