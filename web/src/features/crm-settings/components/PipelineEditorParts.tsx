import { ArrowDown, ArrowUp, ArchiveRestore, Archive, History, Plus, Trash2 } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
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
  id?: number
  versionNumber: number
  status: 'Draft' | 'Published' | 'Superseded' | 'Archived'
  publishedAt?: string | null
  stages?: readonly unknown[]
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

const dateFormat = (value: string | null | undefined) => (value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value)) : null)

/** Which version the board uses, whether what is on screen is live or an unpublished draft, and the history behind both. */
export function PipelineVersionStrip({ versions, dirty, onDiscardDraft, discarding }: VersionStripProps) {
  const { t } = useTranslation('opportunities')
  const { published, draft } = versionState(versions)
  const history = versions.toSorted((a, b) => b.versionNumber - a.versionNumber)
  return (
    <div className="bg-muted/30 flex flex-col gap-1.5 rounded-[var(--nx-r-ctl)] border px-3.5 py-2" data-testid="pipeline-version-strip">
      <div className="flex flex-wrap items-center gap-2">
        {published ? <Badge variant="success">{t('settings.versionStrip.live', { number: published.versionNumber })}</Badge> : <Badge variant="secondary">{t('settings.versionStrip.noneLive')}</Badge>}
        {draft && <Badge variant="warning">{t('settings.versionStrip.draft', { number: draft.versionNumber })}</Badge>}
        {dirty && <Badge variant="info">{t('settings.versionStrip.unsaved')}</Badge>}
        <span className="flex-1" />
        {draft && <Button type="button" variant="ghost" size="sm" disabled={discarding} onClick={onDiscardDraft}><Trash2 aria-hidden strokeWidth={1.7} />{t('settings.versionStrip.discard')}</Button>}
      </div>
      <details className="group text-[12.5px]">
        <summary className="flex cursor-pointer list-none flex-wrap items-center gap-x-3 gap-y-1 [&::-webkit-details-marker]:hidden">
          <span className="text-muted-foreground min-w-0 flex-1">
            {draft
              ? published ? t('settings.versionStrip.draftHint', { number: published.versionNumber }) : t('settings.versionStrip.draftHintNoLive')
              : published ? t('settings.versionStrip.editingLive', { number: published.versionNumber }) : t('settings.versionStrip.newHint')}
          </span>
          <span className="text-[var(--nx-tint)] flex shrink-0 items-center gap-1.5 font-[550]"><History aria-hidden className="size-3.5" strokeWidth={1.8} />{t('settings.versionStrip.history', { count: versions.length })}</span>
        </summary>
        <div className="mt-2 flex flex-col gap-2">
          <ul className="divide-y rounded-[var(--nx-r-ctl)] border bg-background/60">
            {history.map((version) => (
              <li key={version.id ?? version.versionNumber} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2">
                <span className="tnum w-8 font-[590]">v{version.versionNumber}</span>
                <Badge variant={version.status === 'Published' ? 'success' : version.status === 'Draft' ? 'warning' : 'secondary'}>{t(`settings.versionStrip.status.${version.status}`)}</Badge>
                <span className="text-muted-foreground flex-1">{t(`settings.versionStrip.statusHint.${version.status}`)}</span>
                {dateFormat(version.publishedAt) && <span className="text-muted-foreground tnum">{dateFormat(version.publishedAt)}</span>}
              </li>
            ))}
          </ul>
          <p className="text-muted-foreground">{t('settings.pipelineVersionImpact')}</p>
        </div>
      </details>
    </div>
  )
}

interface StageRowProps {
  stage: StageForm
  /** Position among the ordinary stages, zero-based. */
  position: number
  count: number
  /** Move keyboard focus into this row's name field (a stage was just added here). */
  focusName: boolean
  onFocused: () => void
  onName: (name: string) => void
  onMove: (direction: -1 | 1) => void
  onEntry: () => void
  onActive: (active: boolean) => void
  onToggleArchive: () => void
  onInsertBelow: () => void
  onRemove: () => void
}

/** Shared column template, so the header row and every stage row line up. */
export const STAGE_GRID = 'grid grid-cols-[1.5rem_minmax(6rem,1fr)_3.25rem_3.25rem] items-center gap-x-2 gap-y-1.5 md:grid-cols-[1.5rem_minmax(8rem,1fr)_3.25rem_3.25rem_13.5rem]'

export function StageListHeader() {
  const { t } = useTranslation('opportunities')
  return (
    <div aria-hidden className={cn(STAGE_GRID, 'text-muted-foreground px-2.5 text-[11.5px] font-medium')}>
      <span />
      <span>{t('settings.stage')}</span>
      <span className="text-center">{t('settings.entryShort')}</span>
      <span className="text-center">{t('settings.active')}</span>
      <span />
    </div>
  )
}

const iconButton = 'size-8'

export function StageRow({ stage, position, count, focusName, onFocused, onName, onMove, onEntry, onActive, onToggleArchive, onInsertBelow, onRemove }: StageRowProps) {
  const { t } = useTranslation('opportunities')
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (!focusName) return
    input.current?.focus()
    onFocused()
  }, [focusName, onFocused])
  const label = `${t('settings.stage')} ${position + 1}`
  return (
    <div className={cn(STAGE_GRID, 'rounded-[var(--nx-r-ctl)] border px-2.5 py-1.5', stage.isArchived && 'bg-muted/40')} data-stage-kind="Open">
      <span className="text-muted-foreground tnum text-center text-[12px] font-[590]" aria-hidden>{position + 1}</span>
      <Input ref={input} aria-label={label} value={stage.name} required maxLength={100} className={cn('h-8', stage.isArchived && 'line-through opacity-70')} onChange={(event) => onName(event.target.value)} />
      <label className="flex justify-center" title={t('settings.entryStage')}>
        <input type="radio" name="pipeline-entry-stage" aria-label={`${label}: ${t('settings.entryStage')}`} checked={stage.isEntry} disabled={stage.isArchived} onChange={onEntry} className="accent-[var(--nx-tint)] size-4" />
      </label>
      <label className="flex justify-center" title={t('settings.active')}>
        <Checkbox aria-label={`${label}: ${t('settings.active')}`} checked={stage.isActive} disabled={stage.isArchived} onChange={(event) => onActive(event.currentTarget.checked)} />
      </label>
      <div className="col-span-4 flex flex-wrap items-center justify-end gap-1 md:col-span-1">
        <Button type="button" variant="ghost" size="icon" className={iconButton} disabled={position === 0} aria-label={t('settings.moveUp')} title={t('settings.moveUp')} onClick={() => onMove(-1)}><ArrowUp aria-hidden strokeWidth={1.7} /></Button>
        <Button type="button" variant="ghost" size="icon" className={iconButton} disabled={position === count - 1} aria-label={t('settings.moveDown')} title={t('settings.moveDown')} onClick={() => onMove(1)}><ArrowDown aria-hidden strokeWidth={1.7} /></Button>
        <Button type="button" variant="outline" size="icon" className={cn(iconButton, 'border-[var(--nx-st-green-fg)]/40 bg-[var(--nx-st-green-bg)] text-[var(--nx-st-green-fg)] hover:bg-[var(--nx-st-green-bg)] hover:brightness-95')} aria-label={t('settings.insertBelow', { position: position + 1 })} title={t('settings.insertBelow', { position: position + 1 })} onClick={onInsertBelow}><Plus aria-hidden strokeWidth={2} /></Button>
        <Button type="button" variant="ghost" size="icon" className={iconButton} onClick={onToggleArchive} disabled={stage.isEntry} aria-label={stage.isArchived ? t('settings.unarchiveStage') : t('settings.archiveStage')} title={stage.isEntry ? t('settings.entryCannotArchive') : stage.isArchived ? t('settings.unarchiveStage') : t('settings.archiveStage')}>
          {stage.isArchived ? <ArchiveRestore aria-hidden strokeWidth={1.7} /> : <Archive aria-hidden strokeWidth={1.7} />}
        </Button>
        <Button type="button" variant="ghost" size="icon" className={cn(iconButton, 'text-[var(--nx-neg)]')} disabled={count <= 1} aria-label={t('settings.removeStage')} title={t('settings.removeStage')} onClick={onRemove}><Trash2 aria-hidden strokeWidth={1.7} /></Button>
      </div>
    </div>
  )
}

export function SystemStageRow({ stage, onRename }: { stage: StageForm; onRename: (name: string) => void }) {
  const { t } = useTranslation('opportunities')
  const key = stage.kind === 'Won' ? 'won' : 'lost'
  return (
    <div className="bg-muted/30 flex flex-wrap items-center gap-x-3 gap-y-1.5 rounded-[var(--nx-r-ctl)] border border-dashed px-2.5 py-1.5" data-stage-kind={stage.kind}>
      <Badge variant="secondary">{t('settings.systemStage.badge')}</Badge>
      <Input aria-label={t(`settings.systemStage.${key}`)} title={t(`settings.systemStage.${key}Hint`)} value={stage.name} required maxLength={100} className="h-8 min-w-[10rem] flex-1" onChange={(event) => onRename(event.target.value)} />
      <span className="text-muted-foreground hidden text-[11.5px] lg:inline">{t(`settings.systemStage.${key}Short`)}</span>
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
