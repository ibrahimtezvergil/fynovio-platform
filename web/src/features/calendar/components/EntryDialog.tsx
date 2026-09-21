import { CalendarClock, ExternalLink, Link2, Loader2, StickyNote } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useOverlayClose } from '@/lib/overlay'
import { calendarKeys, findCachedEntry, useCalendarIdentity, useDeleteEntry } from '../api'
import { linkFallbackLabel, presentLink } from '../lib/links'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { formatWhen } from '../lib/time'
import type { CalendarEntry } from '../schema'
import { EntryForm, type EntryPrefill } from './EntryForm'
import { ProblemNotice } from './ProblemNotice'

interface EntryDialogProps {
  /** The entry to show; absent opens the create form. */
  entry?: CalendarEntry
  /** Starting values for a new entry (a selected range, a record to link). */
  prefill?: EntryPrefill
}

type Mode = 'view' | 'edit' | 'confirmDelete'

/**
 * One overlay for the whole life of an entry: view → edit → confirm delete are modes of the same dialog, not stacked
 * dialogs, so focus stays inside a single trap, Escape always closes the one thing on screen, and the overlay stack
 * (depth 2) is never exhausted.
 */
export function EntryDialog({ entry: initialEntry, prefill }: EntryDialogProps) {
  const close = useOverlayClose()
  const queryClient = useQueryClient()
  const identity = useCalendarIdentity()
  const [entry, setEntry] = useState(initialEntry)
  const [mode, setMode] = useState<Mode>(initialEntry ? 'view' : 'edit')

  /** Re-reads the list, then shows what the server now holds — or closes if the entry is gone. */
  const reload = async () => {
    const key = calendarKeys.entries(identity.tenantId, identity.principalId)
    await queryClient.invalidateQueries({ queryKey: key })
    const fresh = entry ? findCachedEntry(queryClient, key, entry.id) : undefined
    if (fresh) {
      setEntry(fresh)
      setMode('view')
    } else {
      close()
    }
  }

  if (mode === 'edit') {
    return (
      <EntryForm
        entry={entry}
        prefill={prefill}
        onDone={close}
        onCancel={() => (entry ? setMode('view') : close())}
        onReload={() => void reload()}
      />
    )
  }
  if (!entry) return null
  if (mode === 'confirmDelete') {
    return <DeleteConfirm entry={entry} onDone={close} onCancel={() => setMode('view')} onReload={() => void reload()} />
  }
  return <EntryDetail entry={entry} onClose={close} onEdit={() => setMode('edit')} onDelete={() => setMode('confirmDelete')} />
}

interface EntryDetailProps {
  entry: CalendarEntry
  onClose: () => void
  onEdit: () => void
  onDelete: () => void
}

function EntryDetail({ entry, onClose, onEdit, onDelete }: EntryDetailProps) {
  const { t, i18n } = useTranslation('calendar')
  const link = presentLink(entry.link, (ref) => linkFallbackLabel(t, ref))

  return (
    <>
      <DialogHeader>
        <DialogTitle className="pr-8">{entry.title}</DialogTitle>
        <DialogDescription>{formatWhen(entry, i18n.language)}</DialogDescription>
      </DialogHeader>

      <div className="flex flex-col gap-2.5">
        <div className="flex items-center gap-2.5">
          <CalendarClock aria-hidden className="text-muted-foreground size-3.5 shrink-0" strokeWidth={1.75} />
          <span className="text-[13px]">{entry.allDay ? t('allDay') : t('timed')}</span>
        </div>
        {/* The colour is the person's own tag, not a status: it is printed as its value too, so it never stands alone. */}
        <div className="flex items-center gap-2.5">
          <span aria-hidden className="size-3.5 shrink-0 rounded-full border border-[var(--nx-hairline-strong)]" style={{ background: entry.color }} />
          <span className="text-[13px]">
            {t('detail.color')}: <span className="font-mono text-[12px]">{entry.color}</span>
          </span>
        </div>

        {link.kind === 'route' && (
          <Link to={link.to} onClick={onClose} className="flex items-center gap-2.5 text-[13px] underline-offset-2 hover:underline">
            <ExternalLink aria-hidden className="text-muted-foreground size-3.5 shrink-0" strokeWidth={1.75} />
            <span>
              {t('link.open')}: {link.label}
              {link.subtitle && <span className="text-muted-foreground"> · {link.subtitle}</span>}
            </span>
          </Link>
        )}
        {link.kind === 'label' && (
          <div className="flex items-center gap-2.5">
            <Link2 aria-hidden className="text-muted-foreground size-3.5 shrink-0" strokeWidth={1.75} />
            <span className="text-[13px]">
              {t('link.linked')}: {link.label}
              {link.subtitle && <span className="text-muted-foreground"> · {link.subtitle}</span>}
            </span>
          </div>
        )}
      </div>

      {entry.notes && (
        <p className="text-muted-foreground border-border flex gap-2.5 border-t pt-3.5 text-[12.5px] leading-[1.55] whitespace-pre-wrap">
          <StickyNote aria-hidden className="mt-0.5 size-3.5 shrink-0" strokeWidth={1.75} />
          <span>{entry.notes}</span>
        </p>
      )}

      <DialogFooter>
        <Button type="button" variant="ghost" size="sm" onClick={onClose}>
          {t('close')}
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={onDelete}>
          {t('delete.action')}
        </Button>
        <Button type="button" size="sm" onClick={onEdit}>
          {t('edit')}
        </Button>
      </DialogFooter>
    </>
  )
}

interface DeleteConfirmProps {
  entry: CalendarEntry
  onDone: () => void
  onCancel: () => void
  onReload: () => void
}

function DeleteConfirm({ entry, onDone, onCancel, onReload }: DeleteConfirmProps) {
  const { t } = useTranslation('calendar')
  const command = useKeyedCommand(useDeleteEntry())

  const confirm = async () => {
    if (await command.run({ id: entry.id, expectedVersion: entry.rowVersion })) onDone()
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle className="pr-8">{t('delete.title')}</DialogTitle>
        <DialogDescription>{t('delete.description', { title: entry.title })}</DialogDescription>
      </DialogHeader>
      {command.problem && <ProblemNotice problem={command.problem} onReload={onReload} />}
      <DialogFooter>
        <Button type="button" variant="ghost" size="sm" disabled={command.isPending} onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="button" variant="destructive" size="sm" disabled={command.isPending} onClick={() => void confirm()}>
          {command.isPending && <Loader2 aria-hidden className="animate-spin" />}
          {t('delete.confirm')}
        </Button>
      </DialogFooter>
    </>
  )
}
