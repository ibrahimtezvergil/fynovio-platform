import { Bookmark, BookmarkCheck, Plus, X } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { SAVED_VIEWS } from '@/features/demo-filters/data/views'
import type { SavedView } from '@/features/demo-filters/types'
import { cn } from '@/lib/utils'

interface SavedViewsProps {
  /** null once the user edits anything — an edited view is no longer that view. */
  activeId: string | null
  onSelect: (view: SavedView) => void
  personalViews: readonly SavedView[]
  onSaveCurrent: (label: string) => void
  onRemovePersonal: (id: string) => void
}

/**
 * Named filter+sort combinations: the curated `SAVED_VIEWS` plus whatever the
 * user has saved from the current query (persisted to this browser — see
 * `lib/personalViews.ts`).
 *
 * Selecting one replaces the whole query, and touching any control afterwards
 * clears the selection: a view that stays highlighted while its filters have
 * been edited is the fastest way to make someone trust the wrong list.
 */
export function SavedViews({
  activeId,
  onSelect,
  personalViews,
  onSaveCurrent,
  onRemovePersonal,
}: SavedViewsProps) {
  const { t } = useTranslation('demo-filters')
  const [draftLabel, setDraftLabel] = useState('')
  const [popoverOpen, setPopoverOpen] = useState(false)

  const handleSave = () => {
    const label = draftLabel.trim()
    if (!label) return
    onSaveCurrent(label)
    setDraftLabel('')
    setPopoverOpen(false)
  }

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {SAVED_VIEWS.map((view) => (
        <ViewPill key={view.id} view={view} active={view.id === activeId} onSelect={onSelect} />
      ))}
      {personalViews.map((view) => (
        <ViewPill
          key={view.id}
          view={view}
          active={view.id === activeId}
          onSelect={onSelect}
          onRemove={() => onRemovePersonal(view.id)}
        />
      ))}

      {activeId === null && (
        <>
          <span className="text-[var(--nx-label-3)] ml-1 text-[11.5px]">
            {t('savedViews.customView')}
          </span>
          <Popover open={popoverOpen} onOpenChange={setPopoverOpen}>
            <PopoverTrigger
              render={<Button variant="ghost" size="xs" className="rounded-[var(--nx-r-pill)]" />}
            >
              <Plus aria-hidden className="size-3.5" strokeWidth={2} />
              {t('savedViews.saveCurrent')}
            </PopoverTrigger>
            <PopoverContent className="w-64 gap-2.5" align="start">
              <label className="text-[12px] font-[590]" htmlFor="saved-view-label">
                {t('savedViews.nameLabel')}
              </label>
              <Input
                id="saved-view-label"
                value={draftLabel}
                onChange={(event) => setDraftLabel(event.target.value)}
                placeholder={t('savedViews.namePlaceholder')}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') handleSave()
                }}
              />
              <div className="flex justify-end gap-2">
                <Button variant="ghost" size="sm" onClick={() => setPopoverOpen(false)}>
                  {t('savedViews.cancel')}
                </Button>
                <Button size="sm" disabled={!draftLabel.trim()} onClick={handleSave}>
                  {t('savedViews.save')}
                </Button>
              </div>
            </PopoverContent>
          </Popover>
        </>
      )}
    </div>
  )
}

interface ViewPillProps {
  view: SavedView
  active: boolean
  onSelect: (view: SavedView) => void
  /** Present only for personal views — the curated set can't be removed. */
  onRemove?: () => void
}

function ViewPill({ view, active, onSelect, onRemove }: ViewPillProps) {
  const { t } = useTranslation('demo-filters')
  const Icon = active ? BookmarkCheck : Bookmark

  return (
    <span
      className={cn(
        'nx-pill h-[28px] gap-0 pr-1 transition-colors',
        active
          ? 'bg-[var(--nx-tint-fill)] text-[var(--nx-tint)]'
          : 'text-muted-foreground bg-transparent shadow-[inset_0_0_0_1px_var(--nx-hairline-strong)] hover:bg-[var(--nx-fill-hover)]',
      )}
    >
      <button
        type="button"
        title={view.description || undefined}
        aria-pressed={active}
        onClick={() => onSelect(view)}
        className="-my-1 -ml-2.5 flex h-[28px] cursor-pointer items-center gap-1.5 py-1 pr-1 pl-2.5"
      >
        <Icon aria-hidden className="size-3.5" strokeWidth={1.9} />
        {view.label}
      </button>
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={t('savedViews.removeAria', { label: view.label })}
          className="hover:bg-[var(--nx-fill-hover)] cursor-pointer rounded-full p-0.5"
        >
          <X aria-hidden className="size-3" strokeWidth={2.2} />
        </button>
      )}
    </span>
  )
}
