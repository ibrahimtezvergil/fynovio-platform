import { ArrowDown, ArrowUp, ArchiveRestore, Archive, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import type { PipelineStageKind } from '@/types/schemas'

/** One stage as the editor holds it. Won/Lost (`kind`) are the system's; only their label is the tenant's. */
export interface StageForm {
  id?: number
  name: string
  sortOrder: number
  isEntry: boolean
  isActive: boolean
  isArchived?: boolean
  kind: PipelineStageKind
}

interface VersionLike {
  versionNumber: number
  status: 'Draft' | 'Published' | 'Superseded' | 'Archived'
}

/** The live version and the open draft of a pipeline — the two numbers the person needs to tell them apart. */
export function versionState(versions: readonly VersionLike[]) {
  const latest = (status: VersionLike['status']) => versions.filter((item) => item.status === status).toSorted((a, b) => b.versionNumber - a.versionNumber)[0]
  return { published: latest('Published'), draft: latest('Draft') }
}

interface VersionStripProps {
  versions: readonly VersionLike[]
  dirty: boolean
  onDiscardDraft: () => void
  discarding: boolean
}

/** Which version the board uses, and whether what is on screen is live or an unpublished draft. */
export function PipelineVersionStrip({ versions, dirty, onDiscardDraft, discarding }: VersionStripProps) {
  const { t } = useTranslation('opportunities')
  const { published, draft } = versionState(versions)
  return (
    <div className="bg-muted/30 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-[var(--nx-r-ctl)] border px-4 py-3" data-testid="pipeline-version-strip">
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          {published ? <Badge variant="success">{t('settings.versionStrip.live', { number: published.versionNumber })}</Badge> : <Badge variant="secondary">{t('settings.versionStrip.noneLive')}</Badge>}
          {draft && <Badge variant="warning">{t('settings.versionStrip.draft', { number: draft.versionNumber })}</Badge>}
          {dirty && <Badge variant="info">{t('settings.versionStrip.unsaved')}</Badge>}
        </div>
        <p className="text-muted-foreground text-[12.5px]">
          {draft
            ? published ? t('settings.versionStrip.draftHint', { number: published.versionNumber }) : t('settings.versionStrip.draftHintNoLive')
            : published ? t('settings.versionStrip.editingLive', { number: published.versionNumber }) : t('settings.versionStrip.newHint')}
        </p>
      </div>
      {draft && <Button type="button" variant="outline" size="sm" disabled={discarding} onClick={onDiscardDraft}><Trash2 aria-hidden strokeWidth={1.7} />{t('settings.versionStrip.discard')}</Button>}
    </div>
  )
}

interface StageRowProps {
  stage: StageForm
  /** Position among the ordinary stages, zero-based. */
  position: number
  count: number
  onName: (name: string) => void
  onMove: (direction: -1 | 1) => void
  onEntry: () => void
  onActive: (active: boolean) => void
  onToggleArchive: () => void
  onRemove: () => void
}

export function StageRow({ stage, position, count, onName, onMove, onEntry, onActive, onToggleArchive, onRemove }: StageRowProps) {
  const { t } = useTranslation('opportunities')
  return (
    <div className={cn('flex flex-wrap items-end gap-x-3 gap-y-2 rounded-[var(--nx-r-ctl)] border p-3', stage.isArchived && 'bg-muted/40')} data-stage-kind="Open">
      <div className="min-w-[12rem] flex-1">
        <Field label={`${t('settings.stage')} ${position + 1}`}>{(props) => <Input {...props} value={stage.name} required maxLength={100} onChange={(event) => onName(event.target.value)} />}</Field>
      </div>
      <div className="flex gap-1">
        <Button type="button" variant="outline" size="icon" disabled={position === 0} aria-label={t('settings.moveUp')} onClick={() => onMove(-1)}><ArrowUp aria-hidden strokeWidth={1.7} /></Button>
        <Button type="button" variant="outline" size="icon" disabled={position === count - 1} aria-label={t('settings.moveDown')} onClick={() => onMove(1)}><ArrowDown aria-hidden strokeWidth={1.7} /></Button>
      </div>
      <label className="flex items-center gap-2 pb-2 text-sm">
        <input type="radio" name="pipeline-entry-stage" checked={stage.isEntry} disabled={stage.isArchived} onChange={onEntry} className="accent-[var(--nx-tint)] size-4" />
        {t('settings.entryStage')}
      </label>
      <label className="flex items-center gap-2 pb-2 text-sm">
        <Checkbox checked={stage.isActive} disabled={stage.isArchived} onChange={(event) => onActive(event.currentTarget.checked)} />
        {t('settings.active')}
      </label>
      <Button type="button" variant="outline" onClick={onToggleArchive} disabled={stage.isEntry}>
        {stage.isArchived ? <ArchiveRestore aria-hidden strokeWidth={1.7} /> : <Archive aria-hidden strokeWidth={1.7} />}
        {stage.isArchived ? t('settings.unarchive') : t('settings.archive')}
      </Button>
      <Button type="button" variant="outline" size="icon" disabled={count <= 1} aria-label={t('settings.removeStage')} onClick={onRemove}><Trash2 aria-hidden strokeWidth={1.7} /></Button>
    </div>
  )
}

export function SystemStageRow({ stage, onRename }: { stage: StageForm; onRename: (name: string) => void }) {
  const { t } = useTranslation('opportunities')
  const key = stage.kind === 'Won' ? 'won' : 'lost'
  return (
    <div className="bg-muted/30 grid items-end gap-2 rounded-[var(--nx-r-ctl)] border border-dashed p-3 sm:grid-cols-[1fr_auto]" data-stage-kind={stage.kind}>
      <Field label={t(`settings.systemStage.${key}`)} hint={t(`settings.systemStage.${key}Hint`)}>{(props) => <Input {...props} value={stage.name} required maxLength={100} onChange={(event) => onRename(event.target.value)} />}</Field>
      <Badge variant="secondary" className="mb-2 self-start sm:self-end">{t('settings.systemStage.badge')}</Badge>
    </div>
  )
}

/** "From" down the side, "to" across the top: one grid instead of N×(N−1) loose checkboxes. */
export function TransitionMatrix({ stages, value, onChange }: { stages: readonly StageForm[]; value: Record<string, boolean>; onChange: (key: string, allowed: boolean) => void }) {
  const { t } = useTranslation('opportunities')
  const named = stages.filter((stage) => stage.name.trim() && !stage.isArchived)
  return (
    <div className="flex flex-col gap-2">
      <p className="text-muted-foreground text-[12.5px]">{t('settings.matrixHint')}</p>
      <div className="overflow-x-auto rounded-[var(--nx-r-ctl)] border">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="bg-muted/40">
              <th scope="col" className="text-muted-foreground px-3 py-2 text-left text-[11.5px] font-medium">{t('settings.matrixCorner')}</th>
              {named.map((to) => <th key={to.name} scope="col" className="max-w-32 truncate px-3 py-2 text-center text-[12px] font-medium" title={to.name}>{to.name}</th>)}
            </tr>
          </thead>
          <tbody>
            {named.map((from) => (
              <tr key={from.name} className="border-t">
                <th scope="row" className="max-w-40 truncate px-3 py-2 text-left text-[12px] font-medium" title={from.name}>{from.name}</th>
                {named.map((to) => {
                  const key = `${from.name}:${to.name}`
                  return (
                    <td key={to.name} className="px-3 py-2 text-center">
                      {from.name === to.name
                        ? <span aria-hidden className="text-muted-foreground">—</span>
                        : <Checkbox aria-label={`${from.name} → ${to.name}`} checked={Boolean(value[key])} onChange={(event) => onChange(key, event.currentTarget.checked)} />}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
