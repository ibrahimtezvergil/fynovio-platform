import { ArrowDown, ArrowUp, GripVertical, GitBranch, ListChecks, LoaderCircle, PanelsTopLeft, RotateCcw, Settings2, Trash2, X, XCircle, type LucideIcon } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import { PageHeader } from '@/components/common/PageHeader'
import { Field } from '@/components/common/Field'
import { PageNav, type PageNavItem } from '@/components/common/PageNav'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import { useSessionStore, useTenantSwitchGuard } from '@/lib/auth'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'
import { pipelineStageKindWire, type PipelineStageKind } from '@/types/schemas'
import { useCreatePipelineDraft, useCrmSettings, useManageCrmCatalog, usePublishPipelineVersion, useSetPipelineLifecycle, useUpdateCrmSettings, useValidatePipelineDraft, type CrmCatalogKind } from '../api'
import type { CrmSettings } from '../schema'

type StageForm = { id?: number; name: string; sortOrder: number; isEntry: boolean; isActive: boolean; isArchived?: boolean; kind: PipelineStageKind }
const initialStage = (): StageForm => ({ name: '', sortOrder: 1, isEntry: true, isActive: true, kind: 'Open' })
type SystemKind = Exclude<PipelineStageKind, 'Open'>
type SystemLabels = Record<SystemKind, string>
/** Won and Lost are the system's closing stages: exactly one of each, pinned after the open stages; only their label is the tenant's. */
function withSystemStages(stages: readonly StageForm[], defaults: SystemLabels): StageForm[] {
  const open = stages.filter((stage) => stage.kind === 'Open')
  const system = (['Won', 'Lost'] as const).map((kind): StageForm => stages.find((stage) => stage.kind === kind) ?? { name: defaults[kind], sortOrder: 0, isEntry: false, isActive: true, kind })
  return [...open, ...system.map((stage, index) => ({ ...stage, sortOrder: open.length + index + 1 }))]
}
const openStage = (name: string, index: number): StageForm => ({ name, sortOrder: index + 1, isEntry: index === 0, isActive: true, kind: 'Open' })
type Section = 'general' | 'creation' | 'pipelines' | 'reasons' | 'needs'
const sectionIcons: Record<Section, LucideIcon> = { general: Settings2, creation: PanelsTopLeft, pipelines: GitBranch, reasons: XCircle, needs: ListChecks }
const settingsValues = (value: CrmSettings) => ({ defaultPipelineDefinitionId: value.defaultPipelineDefinitionId,
  opportunityCreationMode: value.opportunityCreationMode, opportunityCreationSteps: value.opportunityCreationSteps, defaultOpportunityTypeId: value.defaultOpportunityTypeId,
  requireLostReason: value.requireLostReason, requireWonLine: value.requireWonLine, defaultAssignmentMode: value.defaultAssignmentMode,
  assignmentPolicy: value.assignmentPolicy, defaultPrincipal: value.defaultPrincipal, defaultTeamId: value.defaultTeamId,
  defaultTerritoryId: value.defaultTerritoryId })

function SystemStageRow({ stage, label, hint, badge, onRename }: { stage: StageForm; label: string; hint: string; badge: string; onRename: (name: string) => void }) {
  return <div className="bg-muted/30 grid items-end gap-2 rounded-[var(--nx-r-ctl)] border border-dashed p-3 lg:grid-cols-[1fr_auto]" data-stage-kind={stage.kind}>
    <Field label={label} hint={hint}>{(props) => <Input {...props} value={stage.name} required maxLength={100} onChange={(event) => onRename(event.target.value)} />}</Field>
    <Badge variant="secondary" className="mb-2 self-start lg:self-end">{badge}</Badge>
  </div>
}

function SectionHeading({ title, description }: { title: string; description: string }) {
  return <div className="flex min-w-0 flex-col gap-0.5"><h2 className="font-heading text-[17px] leading-tight font-[620] tracking-[-0.024em]">{title}</h2><p className="text-muted-foreground text-[12.5px]">{description}</p></div>
}

export default function CrmSettingsPage() {
  const { t } = useTranslation('opportunities')
  const { section: routeSection } = useParams<{ section?: string }>()
  const section: Section = routeSection === 'creation' || routeSection === 'pipelines' || routeSection === 'reasons' || routeSection === 'needs' ? routeSection : 'general'
  const sections: readonly PageNavItem[] = (Object.keys(sectionIcons) as Section[]).map((item) => ({
    to: item === 'general' ? paths.crmSettings : paths.crmSettingsSection(item), label: t(`settings.sections.${item}`), icon: sectionIcons[item],
  }))
  const activeTenantId = useSessionStore((state) => state.activeTenantId)
  const query = useCrmSettings()
  const update = useUpdateCrmSettings()
  const createDraft = useCreatePipelineDraft()
  const publish = usePublishPipelineVersion()
  const lifecycle = useSetPipelineLifecycle()
  const keys = useAttemptKeys()
  const [form, setForm] = useState<CrmSettings | null>(null)
  const pointerDraggedStep = useRef<CrmSettings['opportunityCreationSteps'][number] | null>(null)
  const [pipelineId, setPipelineId] = useState<number | null>(null)
  const [pipelineName, setPipelineName] = useState('')
  const systemLabels = useMemo<SystemLabels>(() => ({ Won: t('settings.systemStage.defaultWon'), Lost: t('settings.systemStage.defaultLost') }), [t])
  const [stages, setStages] = useState<StageForm[]>(() => withSystemStages([initialStage()], systemLabels))
  const [enforceTransitions, setEnforceTransitions] = useState(false)
  const [transitions, setTransitions] = useState<Record<string, boolean>>({})
  const [pipelineBaseline, setPipelineBaseline] = useState<string | null>(null)
  const selectedEditorPipeline = useRef<number | null>(null)
  const [message, setMessage] = useState('')
  const [errorMessage, setErrorMessage] = useState('')
  const [archivePipeline, setArchivePipeline] = useState(false)
  const [pendingPipelineId, setPendingPipelineId] = useState<number | null | undefined>(undefined)
  const [switchConfirmationOpen, setSwitchConfirmationOpen] = useState(false)
  const pendingSwitch = useRef<((proceed: boolean) => void) | null>(null)
  const setTenantSwitchConfirmation = useTenantSwitchGuard((state) => state.setConfirmation)
  const isSettingsDirty = Boolean(form && query.data && JSON.stringify(settingsValues(form)) !== JSON.stringify(settingsValues(query.data)))
  const pipelineSignature = JSON.stringify({ pipelineName, stages, enforceTransitions, transitions })
  const isPipelineDirty = pipelineBaseline !== null && pipelineSignature !== pipelineBaseline
  const hasUnsavedChanges = isSettingsDirty || isPipelineDirty
  const version = query.data?.pipelines.find((pipeline) => pipeline.id === pipelineId)?.versions.find((item) => item.status === 'Draft')
  const validation = useValidatePipelineDraft(pipelineId, version?.id ?? null)

  useEffect(() => { setForm(null); setPipelineId(null); setPipelineBaseline(null); selectedEditorPipeline.current = null; setMessage(''); setErrorMessage('') }, [activeTenantId])
  useEffect(() => { if (query.data && !isSettingsDirty) setForm(query.data) }, [query.data, isSettingsDirty])
  useEffect(() => {
    const clearPointerDrag = () => { pointerDraggedStep.current = null }
    window.addEventListener('pointerup', clearPointerDrag)
    window.addEventListener('pointercancel', clearPointerDrag)
    return () => { window.removeEventListener('pointerup', clearPointerDrag); window.removeEventListener('pointercancel', clearPointerDrag) }
  }, [])
  const confirmTenantSwitch = useCallback(() => {
    if (!hasUnsavedChanges || pendingSwitch.current) return Promise.resolve(!pendingSwitch.current)
    return new Promise<boolean>((resolve) => { pendingSwitch.current = resolve; setSwitchConfirmationOpen(true) })
  }, [hasUnsavedChanges])
  const answerTenantSwitch = useCallback((proceed: boolean) => {
    pendingSwitch.current?.(proceed); pendingSwitch.current = null; setSwitchConfirmationOpen(false)
  }, [])
  useEffect(() => {
    setTenantSwitchConfirmation(confirmTenantSwitch)
    return () => { setTenantSwitchConfirmation(null); pendingSwitch.current?.(false); pendingSwitch.current = null }
  }, [confirmTenantSwitch, setTenantSwitchConfirmation])
  useEffect(() => {
    if (!hasUnsavedChanges) return
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = true }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [hasUnsavedChanges])
  useEffect(() => {
    if (selectedEditorPipeline.current === pipelineId && isPipelineDirty) return
    selectedEditorPipeline.current = pipelineId
    const selected = query.data?.pipelines.find((pipeline) => pipeline.id === pipelineId)
    const nextName = selected?.name ?? ''
    const draft = selected?.versions.find((item) => item.status === 'Draft')
    const basis = draft ?? selected?.versions.find((item) => item.status === 'Published')
    const nextStages = withSystemStages(basis ? basis.stages.map((stage) => ({ id: stage.id, name: stage.name, sortOrder: stage.sortOrder, isEntry: stage.isEntry, isActive: stage.isActive, isArchived: stage.isArchived, kind: stage.kind })) : [initialStage()], systemLabels)
    const nextTransitions = basis ? (() => {
      const stageNames = new Map(basis.stages.map((stage) => [stage.id, stage.name]))
      return Object.fromEntries(basis.allowedTransitions.flatMap((edge) => {
        const from = stageNames.get(edge.fromStageId); const to = stageNames.get(edge.toStageId)
        return from && to ? [[`${from}:${to}`, true]] : []
      }))
    })() : {}
    setPipelineName(nextName)
    setStages(nextStages)
    setEnforceTransitions(basis?.enforceAllowedTransitions ?? false)
    setTransitions(nextTransitions)
    setPipelineBaseline(JSON.stringify({ pipelineName: nextName, stages: nextStages, enforceTransitions: basis?.enforceAllowedTransitions ?? false, transitions: nextTransitions }))
  }, [pipelineId, query.data, isPipelineDirty, systemLabels])

  const mutateSettings = async (event: FormEvent) => {
    event.preventDefault(); if (!form) return
    const payload = { defaultPipelineDefinitionId: form.defaultPipelineDefinitionId, opportunityCreationMode: form.opportunityCreationMode,
      opportunityCreationSteps: form.opportunityCreationSteps,
      defaultOpportunityTypeId: form.defaultOpportunityTypeId, requireLostReason: form.requireLostReason, requireWonLine: form.requireWonLine,
      defaultAssignmentMode: form.defaultAssignmentMode, assignmentPolicy: form.assignmentPolicy, defaultPrincipal: form.defaultPrincipal,
      defaultTeamId: form.defaultTeamId, defaultTerritoryId: form.defaultTerritoryId, expectedVersion: form.rowVersion }
    const key = keys.begin(payload)
    try { await update.mutateAsync({ ...payload, idempotencyKey: key }); keys.settle(null); setErrorMessage(''); setMessage(t('settings.saved')) }
    catch (error) { keys.settle(error as ApiError); setMessage(''); setErrorMessage((error as ApiError).status === 409 ? t('settings.conflict') : t('settings.saveError')) }
  }

  const saveDraft = async (event: FormEvent) => {
    event.preventDefault()
    const openStages = stages.filter((stage) => stage.kind === 'Open' && stage.name.trim()).map((stage, index) => ({ ...stage, sortOrder: index + 1 }))
    const systemStages = stages.filter((stage) => stage.kind !== 'Open').map((stage, index) => ({ ...stage, name: stage.name.trim() || systemLabels[stage.kind as SystemKind], sortOrder: openStages.length + index + 1, isEntry: false, isActive: true, isArchived: false }))
    const body = { pipelineDefinitionId: pipelineId, name: pipelineName.trim(), expectedRowVersion: query.data?.pipelines.find((p) => p.id === pipelineId)?.rowVersion ?? 0,
      expectedLatestVersionNumber: Math.max(0, ...(query.data?.pipelines.find((p) => p.id === pipelineId)?.versions.map((v) => v.versionNumber) ?? [])),
      stages: [...openStages, ...systemStages].map(({ name, sortOrder, isEntry, isActive, isArchived, kind }) => ({ name, sortOrder, isEntry, isActive: isArchived ? false : isActive, isArchived: Boolean(isArchived), kind: pipelineStageKindWire(kind) })), enforceAllowedTransitions: enforceTransitions,
      allowedTransitions: enforceTransitions ? openStages.flatMap((from) => openStages.filter((to) => from.name !== to.name && transitions[`${from.name}:${to.name}`]).map((to) => ({ fromStageName: from.name, toStageName: to.name }))) : [] }
    const key = keys.begin(body)
    try { const result = await createDraft.mutateAsync({ ...body, idempotencyKey: key }); setPipelineBaseline(pipelineSignature); setPipelineId(result.pipelineDefinitionId); keys.settle(null); setErrorMessage(''); setMessage(t('settings.draftSaved')) }
    catch (error) { keys.settle(error as ApiError); setMessage(''); setErrorMessage((error as ApiError).status === 409 ? t('settings.conflict') : (error as ApiError).status === 400 ? t('settings.pipelineInvalid') : t('settings.saveError')) }
  }

  if (query.isPending || (!form && !query.isError)) return <div className="mx-auto w-full max-w-[1320px] animate-pulse space-y-5" aria-label={t('settings.loading')}><div className="h-16 w-80 rounded-[var(--nx-r-card)] bg-muted" /><div className="h-72 rounded-[var(--nx-r-card)] bg-muted" /></div>
  if (query.isError || !form) {
    const forbidden = (query.error as ApiError | null)?.status === 403
    return <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5"><PageHeader eyebrow={t('settings.eyebrow')} title={t('settings.title')} description={t('settings.description')} />
      <Alert variant="destructive"><AlertTitle>{forbidden ? t('settings.forbiddenTitle') : t('settings.loadError')}</AlertTitle><AlertDescription>{forbidden ? t('settings.forbiddenDescription') : t('settings.loadErrorDescription')}</AlertDescription></Alert>
      {!forbidden && <Button type="button" variant="outline" className="self-start" onClick={() => void query.refetch()}>{t('settings.retry')}</Button>}
    </div>
  }
  const set = <K extends keyof CrmSettings>(field: K, value: CrmSettings[K]) => setForm((current) => current ? { ...current, [field]: value } : current)
  const selectedPipeline = form.pipelines.find((item) => item.id === pipelineId)
  const openStages = stages.filter((stage) => stage.kind === 'Open')
  const openStageCount = openStages.length

  const executePipelineLifecycle = async (archive: boolean, restore = false) => {
    if (!selectedPipeline) return
    const body = { pipelineId: selectedPipeline.id, expectedRowVersion: selectedPipeline.rowVersion, isActive: archive || restore ? false : !selectedPipeline.isActive, archive, restore }
    try {
      await lifecycle.mutateAsync({ ...body, idempotencyKey: keys.begin(body) })
      keys.settle(null); setErrorMessage(''); setMessage(archive ? t('settings.archivedMessage') : restore ? t('settings.restoredMessage') : t('settings.saved'))
      if (archive) { setPipelineId(null); setArchivePipeline(false) }
    }
    catch (error) { keys.settle(error as ApiError); setMessage(''); setErrorMessage((error as ApiError).status === 409 ? t('settings.conflict') : t('settings.saveError')) }
  }
  const publishDraft = async () => {
    if (!selectedPipeline || !version) return
    const body = { pipelineId: selectedPipeline.id, versionId: version.id, expectedPipelineRowVersion: selectedPipeline.rowVersion }
    try { await publish.mutateAsync({ ...body, idempotencyKey: keys.begin(body) }); keys.settle(null); setErrorMessage(''); setMessage(t('settings.published')) }
    catch (error) { keys.settle(error as ApiError); setMessage(''); setErrorMessage((error as ApiError).status === 409 ? t('settings.conflict') : t('settings.saveError')) }
  }

  return <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
    <PageHeader eyebrow={t('settings.eyebrow')} title={t('settings.title')} description={t('settings.description')} />
    <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
      <aside className="flex flex-col gap-3 lg:sticky lg:top-[88px]" aria-label={t('settings.sectionNavLabel')}><PageNav items={sections} label={t('settings.sectionNavLabel')} /></aside>
      <div className="flex min-w-0 flex-col gap-4">
    {message && <Alert variant="success"><AlertTitle>{message}</AlertTitle></Alert>}
    {errorMessage && <Alert variant="destructive"><AlertTitle>{errorMessage}</AlertTitle><AlertDescription><Button type="button" variant="outline" size="sm" onClick={() => void query.refetch()}>{t('settings.reload')}</Button></AlertDescription></Alert>}
    {(section === 'general' || section === 'creation') && <form onSubmit={mutateSettings} className="flex flex-col gap-4">
      {section === 'general' && <>
      <Card className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6"><SectionHeading title={t('settings.opportunity.title')} description={t('settings.opportunity.description')} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('settings.defaultPipeline')}>{(props) => <Select {...props} value={form.defaultPipelineDefinitionId ?? ''} onChange={(e) => set('defaultPipelineDefinitionId', e.target.value ? Number(e.target.value) : null)}><option value="">{t('settings.noDefault')}</option>{form.pipelines.filter((p) => p.isActive && !p.isArchived).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</Select>}</Field>
        </div>
        <p className="text-muted-foreground rounded-[var(--nx-r-ctl)] bg-muted/40 px-4 py-3 text-sm">{t('settings.lifecycleReportingHint')}</p>
      </Card>
      </>}
      {section === 'creation' && <Card className="gap-4 px-6 py-6">
        <SectionHeading title={t('settings.creation.title')} description={t('settings.creation.description')} />
        <p className="text-muted-foreground text-sm">{t('settings.creation.hint')}</p>
        <div className="grid gap-3 sm:grid-cols-2" role="radiogroup" aria-label={t('settings.creation.title')}>
          {(['Form', 'Wizard'] as const).map((mode) => {
            const selected = form.opportunityCreationMode === mode
            const option = mode === 'Form' ? 'form' : 'wizard'
            return <button key={mode} type="button" role="radio" aria-checked={selected} onClick={() => set('opportunityCreationMode', mode)} className={`flex min-h-36 flex-col items-start gap-2 rounded-[var(--nx-r-card)] border p-5 text-left transition-colors ${selected ? 'border-primary bg-primary/5 ring-2 ring-primary/20' : 'border-border hover:border-primary/50 hover:bg-muted/40'}`}>
              <span className="flex w-full items-center justify-between gap-3"><span className="font-heading text-base font-semibold">{t(`settings.creation.${option}.title`)}</span><span aria-hidden="true" className={`flex size-5 items-center justify-center rounded-full border ${selected ? 'border-primary' : 'border-muted-foreground/50'}`}>{selected && <span className="size-2.5 rounded-full bg-primary" />}</span></span>
              <span className="text-muted-foreground text-sm">{t(`settings.creation.${option}.description`)}</span>
            </button>
          })}
        </div>
        {form.opportunityCreationMode === 'Wizard' && <div className="mt-2 border-t pt-5">
          <div className="mb-3"><h3 className="font-heading text-sm font-semibold">{t('settings.creation.stepsTitle')}</h3><p className="text-muted-foreground mt-1 text-xs">{t('settings.creation.stepsDescription')}</p></div>
          <ol className="flex flex-col gap-2" aria-label={t('settings.creation.stepsTitle')} onPointerUp={() => { pointerDraggedStep.current = null }} onPointerCancel={() => { pointerDraggedStep.current = null }}>
            {form.opportunityCreationSteps.map((stepName, index) => {
              const stepKey = stepName === 'Customer' ? 'customer' : stepName === 'Needs' ? 'needs' : 'products'
              const label = t(`settings.creation.step.${stepKey}`)
              return <li key={stepName} data-creation-step={stepName} onPointerEnter={() => {
                const moving = pointerDraggedStep.current
                if (!moving || moving === 'Customer' || stepName === 'Customer' || moving === stepName) return
                const from = form.opportunityCreationSteps.indexOf(moving)
                const to = form.opportunityCreationSteps.indexOf(stepName)
                if (from < 0 || to < 1) return
                const reordered = [...form.opportunityCreationSteps]
                reordered.splice(from, 1); reordered.splice(to, 0, moving)
                set('opportunityCreationSteps', reordered)
              }} draggable={stepName !== 'Customer'} onDragStart={(event) => { event.dataTransfer.setData('text/plain', stepName); event.dataTransfer.effectAllowed = 'move' }} onDragOver={(event) => { event.preventDefault(); event.dataTransfer.dropEffect = 'move' }} onDrop={(event) => {
                event.preventDefault()
                const moving = event.dataTransfer.getData('text/plain') as typeof stepName
                const from = form.opportunityCreationSteps.indexOf(moving)
                if (from < 0 || moving === 'Customer' || index === 0) return
                const reordered = [...form.opportunityCreationSteps]
                reordered.splice(from, 1); reordered.splice(index, 0, moving)
                set('opportunityCreationSteps', reordered)
              }} className="flex items-center gap-3 rounded-[var(--nx-r-ctl)] border bg-background px-3 py-3">
                <GripVertical aria-hidden onPointerDown={() => { if (stepName !== 'Customer') pointerDraggedStep.current = stepName }} className={`text-muted-foreground size-4 touch-none ${stepName === 'Customer' ? 'opacity-40' : 'cursor-grab active:cursor-grabbing'}`} />
                <span className="flex-1 text-sm font-medium">{label}</span>
                {stepName === 'Customer' ? <span className="text-muted-foreground text-xs">{t('settings.creation.requiredStep')}</span> : <>
                  <Button type="button" variant="ghost" size="icon" aria-label={t('settings.creation.moveUp')} disabled={index <= 1} onClick={() => { const next = [...form.opportunityCreationSteps]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; set('opportunityCreationSteps', next) }}><ArrowUp aria-hidden /></Button>
                  <Button type="button" variant="ghost" size="icon" aria-label={t('settings.creation.moveDown')} disabled={index === form.opportunityCreationSteps.length - 1} onClick={() => { const next = [...form.opportunityCreationSteps]; [next[index], next[index + 1]] = [next[index + 1], next[index]]; set('opportunityCreationSteps', next) }}><ArrowDown aria-hidden /></Button>
                  <Button type="button" variant="ghost" size="icon" aria-label={t('settings.creation.removeStep', { step: label })} onClick={() => set('opportunityCreationSteps', form.opportunityCreationSteps.filter((item) => item !== stepName))}><X aria-hidden /></Button>
                </>}
              </li>
            })}
          </ol>
          {(['Needs', 'Products'] as const).filter((stepName) => !form.opportunityCreationSteps.includes(stepName)).length > 0 && <div className="mt-3 flex flex-wrap gap-2">
            {(['Needs', 'Products'] as const).filter((stepName) => !form.opportunityCreationSteps.includes(stepName)).map((stepName) => {
              const stepKey = stepName === 'Needs' ? 'needs' : 'products'
              return <Button key={stepName} type="button" variant="outline" onClick={() => set('opportunityCreationSteps', [...form.opportunityCreationSteps, stepName])}>+ {t('settings.creation.addStep', { step: t(`settings.creation.step.${stepKey}`) })}</Button>
            })}
          </div>}
        </div>}
      </Card>}
      {section === 'general' && <>
      <div className="grid gap-4 xl:grid-cols-2">
        <Card className="gap-4 px-6 pt-[22px] pb-6"><SectionHeading title={t('settings.wonRulesTitle')} description={t('settings.wonRulesDescription')} />
          <label className="flex items-start gap-3"><Checkbox checked={form.requireWonLine} onChange={(event) => set('requireWonLine', event.currentTarget.checked)} aria-describedby="crm-won-rule-help" /><span className="text-sm">{t('settings.requireWonLine')}<span id="crm-won-rule-help" className="text-muted-foreground mt-1 block text-xs">{t('settings.wonRuleHelp')}</span></span></label>
        </Card>
        <Card className="gap-4 px-6 pt-[22px] pb-6"><SectionHeading title={t('settings.lostRulesTitle')} description={t('settings.lostRulesDescription')} />
          <label className="flex items-start gap-3"><Checkbox checked={form.requireLostReason} onChange={(event) => set('requireLostReason', event.currentTarget.checked)} aria-describedby="crm-lost-rule-help" /><span className="text-sm">{t('settings.opportunity.requireLostReason')}<span id="crm-lost-rule-help" className="text-muted-foreground mt-1 block text-xs">{t('settings.lostRuleHelp')}</span></span></label>
        </Card>
      </div>
      <Card className="gap-3 px-6 pt-[22px] pb-6"><SectionHeading title={t('settings.assignmentSummaryTitle')} description={t('settings.assignmentSummaryDescription')} /><p className="text-sm font-medium">{form.defaultAssignmentMode === 'Manual' ? t('settings.creatorOwnsOpportunity') : t('settings.legacyAssignmentNotice')}</p></Card>
      </>}
      <div className="nx-material sticky bottom-4 z-[4] flex flex-wrap items-center gap-3 rounded-[var(--nx-r-card)] px-5 py-3.5">
        <span aria-live="polite" className="text-muted-foreground flex-1 text-[12.5px]">{isSettingsDirty ? t('settings.unsaved') : t('settings.allSaved')}</span>
        <Button type="button" variant="ghost" disabled={!isSettingsDirty || update.isPending} onClick={() => { setForm(query.data ?? null); setErrorMessage('') }}><RotateCcw aria-hidden strokeWidth={1.8} />{t('settings.discard')}</Button>
        <Button type="submit" disabled={!isSettingsDirty || update.isPending}>{update.isPending ? <LoaderCircle aria-hidden className="animate-spin" /> : null}{update.isPending ? t('settings.saving') : t('settings.save')}</Button>
      </div>
    </form>}

    {section === 'pipelines' && <Card className="gap-5 px-6 pt-[22px] pb-6"><SectionHeading title={t('settings.pipelineTitle')} description={t('settings.pipelineDescription')} />
      <Field label={t('settings.selectPipeline')}>{(props) => <Select {...props} value={pipelineId ?? ''} onChange={(e) => { const nextId = e.target.value ? Number(e.target.value) : null; if (isPipelineDirty) setPendingPipelineId(nextId); else setPipelineId(nextId) }}><option value="">{t('settings.newPipeline')}</option>{form.pipelines.map((p) => <option key={p.id} value={p.id}>{p.name}{p.isArchived ? ` · ${t('settings.archived')}` : ''}</option>)}</Select>}</Field>
      {selectedPipeline && <div className="flex flex-wrap items-center gap-2"><Badge variant={selectedPipeline.isArchived ? 'secondary' : selectedPipeline.isActive ? 'success' : 'secondary'}>{selectedPipeline.isArchived ? t('settings.status.Archived') : selectedPipeline.isActive ? t('settings.status.Active') : t('settings.status.Inactive')}</Badge><span className="text-muted-foreground text-xs">{t('settings.versionCount', { count: selectedPipeline.versions.length })}</span></div>}
      {selectedPipeline?.isArchived && <Alert variant="info"><AlertDescription>{t('settings.archivedPipelineReadOnly')}</AlertDescription></Alert>}
      <form onSubmit={saveDraft}><fieldset disabled={Boolean(selectedPipeline?.isArchived)} className="flex flex-col gap-4"><Field label={t('settings.pipelineName')}>{(props) => <Input {...props} value={pipelineName} required onChange={(e) => setPipelineName(e.target.value)} />}</Field>
        {!selectedPipeline && <div className="flex flex-wrap gap-2" aria-label={t('settings.pipelineTemplatesLabel')}>
          <Button type="button" variant="outline" onClick={() => setStages(withSystemStages([t('settings.templates.quick.first'), t('settings.templates.quick.second')].map(openStage), systemLabels))}>{t('settings.templates.quick.label')}</Button>
          <Button type="button" variant="outline" onClick={() => setStages(withSystemStages([t('settings.templates.enterprise.first'), t('settings.templates.enterprise.second'), t('settings.templates.enterprise.third')].map(openStage), systemLabels))}>{t('settings.templates.enterprise.label')}</Button>
          <Button type="button" variant="outline" onClick={() => setStages(withSystemStages([t('settings.templates.renewal.first'), t('settings.templates.renewal.second'), t('settings.templates.renewal.third')].map(openStage), systemLabels))}>{t('settings.templates.renewal.label')}</Button>
        </div>}
        <div className="flex flex-col gap-3">{stages.map((stage, index) => stage.kind !== 'Open' ? <SystemStageRow key={stage.kind} stage={stage} label={t(`settings.systemStage.${stage.kind === 'Won' ? 'won' : 'lost'}`)} hint={t(`settings.systemStage.${stage.kind === 'Won' ? 'wonHint' : 'lostHint'}`)} badge={t('settings.systemStage.badge')} onRename={(name) => setStages((all) => all.map((item, i) => i === index ? { ...item, name } : item))} /> : <div key={index} className="grid items-end gap-2 rounded-[var(--nx-r-ctl)] border p-3 lg:grid-cols-[1fr_auto_auto_auto_auto_auto]">
          <Field label={`${t('settings.stage')} ${index + 1}`}>{(props) => <Input {...props} value={stage.name} required onChange={(e) => setStages((all) => all.map((item, i) => i === index ? { ...item, name: e.target.value } : item))} />}</Field>
          <div className="flex gap-1 pb-1"><Button type="button" variant="outline" disabled={index === 0} aria-label={t('settings.moveUp')} onClick={() => setStages((all) => { const next = [...all]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; return next })}>↑</Button><Button type="button" variant="outline" disabled={index === openStageCount - 1} aria-label={t('settings.moveDown')} onClick={() => setStages((all) => { const next = [...all]; [next[index], next[index + 1]] = [next[index + 1], next[index]]; return next })}>↓</Button></div>
          <label className="flex items-center gap-2 pb-2"><Checkbox checked={stage.isEntry} onChange={(event) => { const checked = event.currentTarget.checked; setStages((all) => all.map((item, i) => ({ ...item, isEntry: checked && i === index }))) }} />{t('settings.entryStage')}</label>
          <label className="flex items-center gap-2 pb-2"><Checkbox checked={stage.isActive} onChange={(event) => { const checked = event.currentTarget.checked; setStages((all) => all.map((item, i) => i === index ? { ...item, isActive: checked } : item)) }} />{t('settings.active')}</label>
          <Button type="button" variant="outline" disabled={Boolean(stage.isArchived)} onClick={() => setStages((all) => all.map((item, i) => i === index ? { ...item, isArchived: true, isActive: false, isEntry: false } : item))}>{stage.isArchived ? t('settings.archived') : t('settings.archive')}</Button>
          <Button type="button" variant="outline" size="icon" disabled={openStageCount <= 1} aria-label={t('settings.removeStage')} onClick={() => setStages((all) => { const kept = all.filter((_, i) => i !== index); const firstOpen = kept.findIndex((item) => item.kind === 'Open'); return kept.some((item) => item.kind === 'Open' && item.isEntry) ? kept : kept.map((item, i) => i === firstOpen ? { ...item, isEntry: true, isActive: true, isArchived: false } : item) })}><Trash2 aria-hidden strokeWidth={1.7} /></Button>
        </div>)}</div>
        <div className="flex flex-wrap gap-3"><Button type="button" variant="outline" onClick={() => setStages((all) => [...all.filter((item) => item.kind === 'Open'), { name: '', sortOrder: openStageCount + 1, isEntry: false, isActive: true, kind: 'Open' }, ...all.filter((item) => item.kind !== 'Open')])}>{t('settings.addStage')}</Button>
          <label className="flex items-center gap-2"><Checkbox checked={enforceTransitions} onChange={(event) => setEnforceTransitions(event.currentTarget.checked)} />{t('settings.enforceTransitions')}</label></div>
        {enforceTransitions && <div className="grid gap-2 sm:grid-cols-2">{openStages.flatMap((from) => openStages.filter((to) => from.name && to.name && from.name !== to.name).map((to) => { const key = `${from.name}:${to.name}`; return <label key={key} className="flex items-center gap-2"><Checkbox checked={Boolean(transitions[key])} onChange={(event) => { const checked = event.currentTarget.checked; setTransitions((all) => ({ ...all, [key]: checked })) }} />{from.name} → {to.name}</label> }))}</div>}
        <Button type="submit" disabled={createDraft.isPending || Boolean(selectedPipeline?.isArchived)}>{t('settings.saveDraft')}</Button>
      </fieldset></form>
      <Alert variant="info"><AlertDescription>{t('settings.pipelineVersionImpact')}</AlertDescription></Alert>
      {version && <div className="flex flex-col gap-3 border-t pt-4"><h3 className="text-sm font-medium">{t('settings.validation')}</h3>
        {isPipelineDirty && <Alert variant="info"><AlertDescription>{t('settings.saveBeforePublish')}</AlertDescription></Alert>}
        {validation.isPending && <p className="text-muted-foreground text-sm">{t('settings.validating')}</p>}
        {validation.isError && <Alert variant="destructive"><AlertTitle>{t('settings.validationError')}</AlertTitle><AlertDescription><Button type="button" variant="outline" size="sm" onClick={() => void validation.refetch()}>{t('settings.retry')}</Button></AlertDescription></Alert>}
        {validation.data && <><Badge variant={validation.data.isValid ? 'success' : 'warning'}>{validation.data.isValid ? t('settings.valid') : t('settings.invalid')}</Badge><p className="text-muted-foreground text-xs">{t('settings.retainedOpportunities', { count: validation.data.opportunitiesRetainedOnPriorVersions })}</p>{validation.data.errors.map((error) => <p role="alert" className="text-[var(--nx-neg)] text-sm" key={error}>{error}</p>)}</>}
        <Button type="button" className="self-start" disabled={isPipelineDirty || !validation.data?.isValid || publish.isPending || !selectedPipeline || selectedPipeline.isArchived} onClick={() => void publishDraft()}>{t('settings.publish')}</Button></div>}
      {selectedPipeline && <div className="flex flex-col gap-3 border-t pt-4">
        {form.defaultPipelineDefinitionId === selectedPipeline.id && <Alert variant="warning"><AlertDescription>{t('settings.defaultPipelineLifecycleBlock')}</AlertDescription></Alert>}
        <div className="flex flex-wrap gap-2">{selectedPipeline.isArchived
          ? <Button type="button" variant="outline" disabled={lifecycle.isPending} onClick={() => void executePipelineLifecycle(false, true)}>{t('settings.restore')}</Button>
          : <><Button type="button" variant="outline" disabled={lifecycle.isPending || isPipelineDirty || (selectedPipeline.isActive && form.defaultPipelineDefinitionId === selectedPipeline.id)} onClick={() => void executePipelineLifecycle(false)}>{selectedPipeline.isActive ? t('settings.deactivate') : t('settings.activate')}</Button><Button type="button" variant="outline" disabled={lifecycle.isPending || isPipelineDirty || form.defaultPipelineDefinitionId === selectedPipeline.id} onClick={() => setArchivePipeline(true)}>{t('settings.archive')}</Button></>}
        </div>
      </div>}
    </Card>}

    {section === 'reasons' && <CatalogPanel kind="lost-reasons" title={t('settings.reasonsTitle')} description={t('settings.reasonsDescription')} items={form.lostReasons} />}
    {section === 'needs' && <CatalogPanel kind="customer-needs" title={t('settings.needsTitle')} description={t('settings.needsDescription')} items={form.customerNeeds} />}
      </div>
    </div>
    <AlertDialog open={archivePipeline} onOpenChange={setArchivePipeline}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{t('settings.confirmArchiveTitle')}</AlertDialogTitle><AlertDialogDescription>{t('settings.confirmArchivePipeline')}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel><AlertDialogAction variant="destructive" onClick={() => void executePipelineLifecycle(true)}>{t('settings.archive')}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    <AlertDialog open={pendingPipelineId !== undefined} onOpenChange={(open) => { if (!open) setPendingPipelineId(undefined) }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{t('settings.unsavedTitle')}</AlertDialogTitle><AlertDialogDescription>{t('settings.unsavedDescription')}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel><AlertDialogAction variant="destructive" onClick={() => { setPipelineId(pendingPipelineId ?? null); setPendingPipelineId(undefined) }}>{t('settings.discardAndSwitch')}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    <AlertDialog open={switchConfirmationOpen} onOpenChange={(open) => { if (!open) answerTenantSwitch(false) }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{t('settings.unsavedTitle')}</AlertDialogTitle><AlertDialogDescription>{t('settings.unsavedDescription')}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel onClick={() => answerTenantSwitch(false)}>{t('common.cancel')}</AlertDialogCancel><AlertDialogAction variant="destructive" onClick={() => answerTenantSwitch(true)}>{t('settings.discardAndSwitch')}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </div>
}

type CatalogItem = { id: number; name: string; key?: string; category?: string | null; averagePrice?: number; status: 'Active' | 'Inactive' | 'Archived'; rowVersion: number }
const catalogKeyFromName = (name: string) => name.trim().normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
  .replace(/ı/g, 'i').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'lost-reason'

function CatalogPanel({ kind, title, description, items }: { kind: CrmCatalogKind; title: string; description: string; items: CatalogItem[] }) {
  const { t } = useTranslation('opportunities')
  const mutation = useManageCrmCatalog(kind)
  const keys = useAttemptKeys()
  const [editing, setEditing] = useState<number | null>(null)
  const [archiveTarget, setArchiveTarget] = useState<CatalogItem | null>(null)
  const [name, setName] = useState('')
  const [keyValue, setKeyValue] = useState('')
  const [category, setCategory] = useState('')
  const [averagePrice, setAveragePrice] = useState('0')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const begin = (item?: CatalogItem) => {
    setEditing(item?.id ?? 0); setName(item?.name ?? ''); setKeyValue(item?.key ?? '')
    setCategory(item?.category ?? ''); setAveragePrice(String(item?.averagePrice ?? 0)); setError(''); setNotice('')
  }
  const failure = (problem: unknown) => { const apiError = problem as ApiError; keys.settle(apiError); setNotice(''); setError(apiError.status === 409 ? t('settings.conflict') : t('settings.saveError')) }
  const save = async (event: FormEvent) => {
    event.preventDefault()
    const item = items.find((candidate) => candidate.id === editing)
    const body = { id: item?.id, expectedVersion: item?.rowVersion ?? 0, name: name.trim(), key: item?.key ?? (kind === 'lost-reasons' ? catalogKeyFromName(name) : keyValue.trim()), category: category.trim() || null, averagePrice: Number(averagePrice), status: item?.status ?? 'Active' as const }
    try { await mutation.mutateAsync({ ...body, idempotencyKey: keys.begin(body) }); keys.settle(null); setEditing(null); setError(''); setNotice(t('settings.saved')) }
    catch (problem) { failure(problem) }
  }
  const setStatus = async (item: CatalogItem, status: CatalogItem['status']) => {
    const body = { id: item.id, expectedVersion: item.rowVersion, name: item.name, key: item.key, category: item.category, averagePrice: item.averagePrice, status }
    try { await mutation.mutateAsync({ ...body, idempotencyKey: keys.begin(body) }); keys.settle(null); setError(''); setNotice(status === 'Archived' ? t('settings.archivedMessage') : t('settings.saved')) }
    catch (problem) { failure(problem) }
  }
  return <Card className="gap-5 px-6 pt-[22px] pb-6">
    <div className="flex flex-wrap items-start justify-between gap-3"><SectionHeading title={title} description={description} /><Button type="button" variant="outline" onClick={() => begin()}>{t('settings.add')}</Button></div>
    {notice && <Alert variant="success"><AlertTitle>{notice}</AlertTitle></Alert>}
    {error && <Alert variant="destructive"><AlertTitle>{error}</AlertTitle></Alert>}
    {items.length === 0 && editing === null && <p className="text-muted-foreground rounded-[var(--nx-r-ctl)] border border-dashed px-4 py-8 text-center text-sm">{t('settings.catalogEmpty')}</p>}
    {items.length > 0 && <div className="divide-y rounded-[var(--nx-r-card)] border">{items.map((item) => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
      <div className="min-w-0"><p className="font-medium">{item.name}</p>{(kind !== 'lost-reasons' && (item.key || item.category)) && <p className="text-muted-foreground text-xs">{[item.key, item.category].filter(Boolean).join(' · ')}</p>}</div>
      <div className="flex flex-wrap items-center gap-2"><Badge variant={item.status === 'Active' ? 'success' : item.status === 'Archived' ? 'secondary' : 'warning'}>{t(`settings.status.${item.status}`)}</Badge>
        <Button type="button" variant="outline" size="sm" disabled={item.status === 'Archived' || mutation.isPending} onClick={() => begin(item)}>{t('settings.edit')}</Button>
        {item.status !== 'Archived' && <><Button type="button" variant="outline" size="sm" disabled={mutation.isPending} onClick={() => void setStatus(item, item.status === 'Active' ? 'Inactive' : 'Active')}>{item.status === 'Active' ? t('settings.deactivate') : t('settings.activate')}</Button><Button type="button" variant="outline" size="sm" disabled={mutation.isPending} onClick={() => setArchiveTarget(item)}>{t('settings.archive')}</Button></>}
      </div>
    </div>)}</div>}
    {editing !== null && <form onSubmit={(event) => void save(event)} className="grid gap-3 border-t pt-4 sm:grid-cols-2">
      <Field label={t('settings.name')}>{(props) => <Input {...props} value={name} required onChange={(event) => setName(event.target.value)} />}</Field>
      {kind !== 'customer-needs' && kind !== 'lost-reasons' && <Field label={t('settings.key')} hint={editing > 0 ? t('settings.keyStable') : undefined}>{(props) => <Input {...props} value={keyValue} required disabled={editing > 0} onChange={(event) => setKeyValue(event.target.value)} />}</Field>}
      {kind === 'customer-needs' && <><Field label={t('settings.category')}>{(props) => <Input {...props} value={category} onChange={(event) => setCategory(event.target.value)} />}</Field><Field label={t('settings.averagePrice')}>{(props) => <Input {...props} type="number" min="0" step="0.01" value={averagePrice} onChange={(event) => setAveragePrice(event.target.value)} />}</Field></>}
      <div className="flex gap-2 sm:col-span-2"><Button type="submit" disabled={mutation.isPending}>{t('settings.save')}</Button><Button type="button" variant="outline" onClick={() => setEditing(null)}>{t('common.cancel')}</Button></div>
    </form>}
    <AlertDialog open={archiveTarget !== null} onOpenChange={(open) => { if (!open) setArchiveTarget(null) }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{t('settings.confirmArchiveTitle')}</AlertDialogTitle><AlertDialogDescription>{t('settings.confirmArchiveCatalog', { name: archiveTarget?.name ?? '' })}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel><AlertDialogAction variant="destructive" onClick={() => { if (archiveTarget) void setStatus(archiveTarget, 'Archived'); setArchiveTarget(null) }}>{t('settings.archive')}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </Card>
}
