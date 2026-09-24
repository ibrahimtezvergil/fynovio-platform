import { MessageSquareText, MoreHorizontal, Pencil } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import type { OpportunityRow } from '../lib/rows'

type QuickNoteKind = 'call' | 'meeting' | 'email' | 'offer' | 'custom'
type EditDraft = { party: string; owner: string; needs: string; amount: string }

export function OpportunityRowActions({ row, onEdit }: { row: OpportunityRow; onEdit: (changes: Partial<OpportunityRow>) => void }) {
  const { t } = useTranslation('opportunities')
  const [dialog, setDialog] = useState<'note' | 'edit' | null>(null)
  const [noteKind, setNoteKind] = useState<QuickNoteKind>('call')
  const [comment, setComment] = useState('')
  const [draft, setDraft] = useState<EditDraft | null>(null)

  const openEdit = () => {
    setDraft({ party: row.party ?? '', owner: row.owner ?? '', needs: row.needs.join(', '), amount: row.amount == null ? '' : String(row.amount) })
    setDialog('edit')
  }

  const saveNote = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    toast.info(t('list.rowActions.notConnected'))
    setDialog(null)
    setComment('')
  }

  const saveEdit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!draft) return
    const amount = Number(draft.amount.replace(',', '.'))
    onEdit({
      party: draft.party.trim() || null,
      owner: draft.owner.trim() || null,
      needs: draft.needs.split(',').map((need) => need.trim()).filter(Boolean),
      amount: Number.isFinite(amount) && draft.amount.trim() ? amount : null,
    })
    toast.info(t('list.rowActions.localEditNotice'))
    setDialog(null)
  }

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger render={<Button type="button" variant="ghost" size="icon-sm" aria-label={t('list.rowActions.open')}><MoreHorizontal aria-hidden /></Button>} />
        <DropdownMenuContent align="end" className="w-52">
          <DropdownMenuItem onClick={() => setDialog('note')}><MessageSquareText aria-hidden strokeWidth={1.7} />{t('list.rowActions.quickNote')}</DropdownMenuItem>
          <DropdownMenuItem onClick={openEdit}><Pencil aria-hidden strokeWidth={1.7} />{t('list.rowActions.edit')}</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog open={dialog === 'note'} onOpenChange={(open) => !open && setDialog(null)}>
        <DialogContent>
          <form onSubmit={saveNote} className="grid gap-4">
            <DialogHeader>
              <DialogTitle>{t('list.rowActions.quickNoteTitle', { id: row.id })}</DialogTitle>
              <DialogDescription>{t('list.rowActions.quickNoteDescription')}</DialogDescription>
            </DialogHeader>
            <label className="grid gap-1.5 text-sm" htmlFor={`quick-note-kind-${row.id}`}>{t('list.rowActions.noteKind')}
              <select id={`quick-note-kind-${row.id}`} className="h-10 rounded-md border border-input bg-background px-3" value={noteKind} onChange={(event) => setNoteKind(event.target.value as QuickNoteKind)}>
                {(['call', 'meeting', 'email', 'offer', 'custom'] as const).map((kind) => <option key={kind} value={kind}>{t(`list.rowActions.noteKinds.${kind}`)}</option>)}
              </select>
            </label>
            {noteKind === 'custom' && <label className="grid gap-1.5 text-sm" htmlFor={`quick-note-comment-${row.id}`}>{t('list.rowActions.customComment')}
              <Textarea id={`quick-note-comment-${row.id}`} required maxLength={2000} value={comment} onChange={(event) => setComment(event.target.value)} />
            </label>}
            <DialogFooter>
              <Button type="button" variant="ghost" onClick={() => setDialog(null)}>{t('common.cancel')}</Button>
              <Button type="submit">{t('list.rowActions.prepareNote')}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'edit'} onOpenChange={(open) => !open && setDialog(null)}>
        <DialogContent>
          <form onSubmit={saveEdit} className="grid gap-4">
            <DialogHeader>
              <DialogTitle>{t('list.rowActions.editTitle', { id: row.id })}</DialogTitle>
              <DialogDescription>{t('list.rowActions.editDescription')}</DialogDescription>
            </DialogHeader>
            {draft && <>
              <label className="grid gap-1.5 text-sm">{t('list.columns.party')}<Input value={draft.party} onChange={(event) => setDraft({ ...draft, party: event.target.value })} /></label>
              <label className="grid gap-1.5 text-sm">{t('list.columns.owner')}<Input value={draft.owner} onChange={(event) => setDraft({ ...draft, owner: event.target.value })} /></label>
              <label className="grid gap-1.5 text-sm">{t('list.columns.needs')}<Input value={draft.needs} onChange={(event) => setDraft({ ...draft, needs: event.target.value })} /></label>
              <label className="grid gap-1.5 text-sm">{t('list.columns.amount')}<Input inputMode="decimal" value={draft.amount} onChange={(event) => setDraft({ ...draft, amount: event.target.value })} /></label>
            </>}
            <DialogFooter>
              <Button type="button" variant="ghost" onClick={() => setDialog(null)}>{t('common.cancel')}</Button>
              <Button type="submit">{t('list.rowActions.applyLocally')}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}
