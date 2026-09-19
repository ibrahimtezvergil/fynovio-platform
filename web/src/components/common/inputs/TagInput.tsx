import { X } from 'lucide-react'
import { useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

export interface TagInputProps extends FieldControlProps {
  value: readonly string[]
  onValueChange: (value: string[]) => void
  placeholder?: string
  maxTags?: number
  disabled?: boolean
  className?: string
}

/**
 * Free-text chips: labels on a deal, keywords on a product, anything with no
 * fixed option list. Enter or comma commits, Backspace on an empty field takes
 * the last one back, and duplicates are dropped rather than rejected loudly.
 */
export function TagInput({
  value,
  onValueChange,
  placeholder,
  maxTags,
  disabled,
  className,
  id,
  ...aria
}: TagInputProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('tagInput.placeholder')
  const [draft, setDraft] = useState('')
  const full = maxTags !== undefined && value.length >= maxTags

  const commit = () => {
    const tag = draft.trim().replace(/,$/, '')
    setDraft('')
    if (tag.length === 0 || full) return
    if (value.some((entry) => entry.toLocaleLowerCase('tr') === tag.toLocaleLowerCase('tr'))) return
    onValueChange([...value, tag])
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault()
      commit()
      return
    }
    if (event.key === 'Backspace' && draft.length === 0 && value.length > 0) {
      onValueChange(value.slice(0, -1))
    }
  }

  return (
    <div
      className={cn(
        CONTROL_SHELL,
        'h-auto min-h-control flex-wrap items-center gap-1 p-1.5',
        className,
      )}
    >
      {value.map((tag) => (
        <span
          key={tag}
          className="bg-accent text-accent-foreground flex h-[26px] items-center gap-1 rounded-sm pr-1 pl-2.5 text-[12.5px] font-[550]"
        >
          {tag}
          <button
            type="button"
            aria-label={t('tagInput.removeTag', { tag })}
            disabled={disabled}
            onClick={() => onValueChange(value.filter((entry) => entry !== tag))}
            className="flex size-[18px] cursor-pointer items-center justify-center rounded-full border-0 bg-transparent p-0 text-inherit outline-none hover:bg-[var(--nx-tint-fill-hover)]"
          >
            <X aria-hidden className="size-3" strokeWidth={2.25} />
          </button>
        </span>
      ))}
      <input
        id={id}
        type="text"
        value={draft}
        placeholder={
          full
            ? t('tagInput.maxTags', { count: maxTags })
            : value.length > 0
              ? ''
              : resolvedPlaceholder
        }
        disabled={disabled || full}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={commit}
        className="text-foreground h-[26px] min-w-32 flex-1 border-0 bg-transparent px-1.5 text-[13.5px] outline-none placeholder:text-[var(--nx-label-3)]"
        {...aria}
      />
    </div>
  )
}
