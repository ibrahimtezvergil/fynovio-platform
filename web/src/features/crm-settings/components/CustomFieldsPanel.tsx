import { MoreHorizontal, Pencil, Plus, Power, PowerOff, Undo2, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Alert, AlertTitle } from '@/components/ui/alert'
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { useCustomFieldDefinitions, useCustomFieldImpact, useManageCustomField, type CustomFieldConfigInput, type ManageCustomFieldVariables } from '@/lib/custom-fields/api'
import { keyFromLabel } from '@/lib/custom-fields/values'
import { CUSTOM_FIELD_TYPES, isOptionType, type CustomFieldDefinition, type CustomFieldType } from '@/lib/custom-fields/schema'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import type { ApiError } from '@/types'
import { SectionHeading } from './SectionHeading'

/** Omit that keeps the union's members apart (a plain Omit collapses them to the shared keys). */
type WithoutKey<T> = T extends unknown ? Omit<T, 'idempotencyKey'> : never
type OptionDraft = { key: string; label: string; isDeprecated: boolean; saved: boolean }
interface EditorState {
  id: number | null
  rowVersion: number
  key: string
  label: string
  type: CustomFieldType
  isRequired: boolean
  sortOrder: string
  maxLength: string
  min: string
  max: string
  scale: string
  options: OptionDraft[]
}

const numberOrNull = (text: string) => (text.trim() === '' ? null : Number(text.replace(',', '.')))

const emptyEditor = (sortOrder: number): EditorState => ({
  id: null, rowVersion: 0, key: '', label: '', type: 'text', isRequired: false, sortOrder: String(sortOrder),
  maxLength: '', min: '', max: '', scale: '2', options: [],
})

const toEditor = (definition: CustomFieldDefinition): EditorState => ({
  id: definition.id, rowVersion: definition.rowVersion, key: definition.fieldName, label: definition.label, type: definition.fieldType,
  isRequired: definition.isRequired, sortOrder: String(definition.sortOrder),
  maxLength: definition.config.maxLength?.toString() ?? '', min: definition.config.min?.toString() ?? '', max: definition.config.max?.toString() ?? '',
  scale: definition.config.scale?.toString() ?? '2',
  options: (definition.config.options ?? []).map((option) => ({ ...option, saved: true })),
})

/** Only the config members the chosen type uses — the server rejects the others. */
function toConfig(editor: EditorState): CustomFieldConfigInput | null {
  switch (editor.type) {
    case 'text':
    case 'long_text':
      return { maxLength: numberOrNull(editor.maxLength) }
    case 'number':
      return { min: numberOrNull(editor.min), max: numberOrNull(editor.max) }
    case 'decimal':
      return { min: numberOrNull(editor.min), max: numberOrNull(editor.max), scale: numberOrNull(editor.scale) }
    case 'reference':
      // The one target v1 allows; fixed at creation, so an edit sends it back unchanged.
      return { target: { boundedContext: 'masterdata', entityType: 'party' } }
    case 'select':
    case 'multi_select':
      return { options: editor.options.filter((option) => option.label.trim() !== '').map((option) => ({
        key: option.key || keyFromLabel(option.label), label: option.label.trim(), isDeprecated: option.isDeprecated,
      })) }
    default:
      return null
  }
}

/**
 * Tenant-defined opportunity fields: a definition's key and type are fixed once created, options are only ever
 * deprecated (never removed), and deprecating a field first shows how many opportunities hold a value for it.
 */
export function CustomFieldsPanel() {
  const { t } = useTranslation('opportunities')
  const definitions = useCustomFieldDefinitions()
  const mutation = useManageCustomField()
  const keys = useAttemptKeys()
  const [editor, setEditor] = useState<EditorState | null>(null)
  const [deprecateTarget, setDeprecateTarget] = useState<CustomFieldDefinition | null>(null)
  const [error, setError] = useState('')
  const impact = useCustomFieldImpact(deprecateTarget?.id ?? null)
  const items = definitions.data ?? []

  const failure = (problem: unknown) => {
    const apiError = problem as ApiError
    keys.settle(apiError)
    setError(apiError.code === 'custom_field_key_conflict' ? t('customFields.settings.keyConflict')
      : apiError.code === 'field_limit_exceeded' ? t('customFields.settings.limit')
        : apiError.code === 'concurrency_conflict' ? t('customFields.settings.conflict')
          : apiError.code === 'validation_error' && apiError.message ? apiError.message
            : t('customFields.settings.saveError'))
  }
  const send = async (variables: WithoutKey<ManageCustomFieldVariables>) => {
    try {
      await mutation.mutateAsync({ ...variables, idempotencyKey: keys.begin(variables) } as ManageCustomFieldVariables)
      keys.settle(null)
      setError('')
      return true
    } catch (problem) {
      failure(problem)
      return false
    }
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!editor) return
    const common = { label: editor.label.trim(), isRequired: editor.isRequired, sortOrder: Number(editor.sortOrder) || 0, config: toConfig(editor) }
    const saved = editor.id === null
      ? await send({ operation: 'create', key: editor.key.trim() || keyFromLabel(editor.label), type: editor.type, ...common })
      : await send({ operation: 'update', id: editor.id, expectedRowVersion: editor.rowVersion, ...common })
    if (saved) setEditor(null)
  }

  const setOption = (index: number, change: Partial<OptionDraft>) =>
    setEditor((current) => current && { ...current, options: current.options.map((option, at) => (at === index ? { ...option, ...change } : option)) })

  const impactCount = impact.data?.opportunitiesWithValue
  const impactViews = impact.data?.dependentViews ?? []
  return (
    <Card className="min-w-0 gap-4 px-6 pt-[22px] pb-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <SectionHeading title={t('customFields.settings.title')} description={t('customFields.settings.description')} />
        <Button type="button" variant="outline" onClick={() => { setError(''); setEditor(emptyEditor((items.at(-1)?.sortOrder ?? 0) + 10)) }}>
          <Plus aria-hidden strokeWidth={1.7} />{t('customFields.settings.add')}
        </Button>
      </div>
      {error && <Alert variant="destructive"><AlertTitle>{error}</AlertTitle></Alert>}

      {definitions.isPending && <Skeleton className="h-24" />}
      {definitions.isSuccess && items.length === 0 && editor === null && (
        <p className="text-muted-foreground rounded-[var(--nx-r-ctl)] border border-dashed px-4 py-8 text-center text-sm">{t('customFields.settings.empty')}</p>
      )}

      {items.length > 0 && (
        <ul className="divide-y rounded-[var(--nx-r-card)] border">
          {items.map((item) => {
            const deprecated = item.status === 'Deprecated'
            return (
              <li key={item.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5">
                <div className={`min-w-0 flex-1 ${deprecated ? 'text-muted-foreground' : ''}`}>
                  <p className="truncate font-medium">{item.label}</p>
                  <p className="text-muted-foreground truncate text-xs">{[item.fieldName, t(`customFields.types.${item.fieldType}`), item.isRequired ? t('customFields.settings.required') : null].filter(Boolean).join(' · ')}</p>
                </div>
                <Badge variant={deprecated ? 'secondary' : 'success'}>{deprecated ? t('customFields.settings.deprecated') : t('customFields.settings.active')}</Badge>
                {!deprecated && (
                  <Button type="button" variant="outline" size="sm" disabled={mutation.isPending} onClick={() => { setError(''); setEditor(toEditor(item)) }}>
                    <Pencil aria-hidden strokeWidth={1.7} />{t('customFields.settings.edit')}
                  </Button>
                )}
                <DropdownMenu>
                  <DropdownMenuTrigger render={<Button type="button" variant="ghost" size="icon-sm" disabled={mutation.isPending} aria-label={t('customFields.settings.more', { name: item.label })}><MoreHorizontal aria-hidden /></Button>} />
                  <DropdownMenuContent align="end" className="w-60">
                    {deprecated
                      ? <DropdownMenuItem onClick={() => void send({ operation: 'reactivate', id: item.id, expectedRowVersion: item.rowVersion })}><Power aria-hidden strokeWidth={1.7} />{t('customFields.settings.reactivate')}</DropdownMenuItem>
                      : <DropdownMenuItem variant="destructive" onClick={() => setDeprecateTarget(item)}><PowerOff aria-hidden strokeWidth={1.7} />{t('customFields.settings.deprecate')}</DropdownMenuItem>}
                  </DropdownMenuContent>
                </DropdownMenu>
              </li>
            )
          })}
        </ul>
      )}

      {editor !== null && (
        <form onSubmit={(event) => void save(event)} className="grid gap-3 border-t pt-4 sm:grid-cols-2" aria-label={editor.id === null ? t('customFields.settings.add') : t('customFields.settings.edit')}>
          <Field label={t('customFields.settings.label')}>
            {(props) => <Input {...props} value={editor.label} required maxLength={100} autoComplete="off" onChange={(event) => setEditor({ ...editor, label: event.target.value })} />}
          </Field>
          <Field label={t('customFields.settings.key')} hint={t('customFields.settings.keyHint')}>
            {(props) => <Input {...props} value={editor.id === null ? editor.key || keyFromLabel(editor.label) : editor.key} disabled={editor.id !== null} maxLength={63} autoComplete="off"
              onChange={(event) => setEditor({ ...editor, key: event.target.value })} />}
          </Field>
          <Field label={t('customFields.settings.type')} hint={editor.id !== null ? t('customFields.settings.typeHint') : undefined}>
            {(props) => (
              <Select {...props} value={editor.type} disabled={editor.id !== null} onChange={(event) => setEditor({ ...editor, type: event.target.value as CustomFieldType })}>
                {CUSTOM_FIELD_TYPES.map((type) => <option key={type} value={type}>{t(`customFields.types.${type}`)}</option>)}
              </Select>
            )}
          </Field>
          <Field label={t('customFields.settings.sortOrder')}>
            {(props) => <Input {...props} type="number" min="0" max="10000" value={editor.sortOrder} onChange={(event) => setEditor({ ...editor, sortOrder: event.target.value })} />}
          </Field>
          {editor.type === 'reference' && (
            <Field label={t('customFields.reference.target')} hint={t('customFields.reference.targetHint')}>
              {(props) => <Select {...props} value="masterdata/party" disabled><option value="masterdata/party">{t('customFields.reference.targetParty')}</option></Select>}
            </Field>
          )}
          {(editor.type === 'text' || editor.type === 'long_text') && (
            <Field label={t('customFields.settings.maxLength')}>
              {(props) => <Input {...props} type="number" min="1" value={editor.maxLength} onChange={(event) => setEditor({ ...editor, maxLength: event.target.value })} />}
            </Field>
          )}
          {(editor.type === 'number' || editor.type === 'decimal') && (
            <>
              <Field label={t('customFields.settings.min')}>{(props) => <Input {...props} inputMode="decimal" value={editor.min} onChange={(event) => setEditor({ ...editor, min: event.target.value })} />}</Field>
              <Field label={t('customFields.settings.max')}>{(props) => <Input {...props} inputMode="decimal" value={editor.max} onChange={(event) => setEditor({ ...editor, max: event.target.value })} />}</Field>
            </>
          )}
          {editor.type === 'decimal' && (
            <Field label={t('customFields.settings.scale')}>
              {(props) => <Input {...props} type="number" min="0" max="6" disabled={editor.id !== null} value={editor.scale} onChange={(event) => setEditor({ ...editor, scale: event.target.value })} />}
            </Field>
          )}
          <label className="flex items-start gap-2.5 text-[13.5px] sm:col-span-2">
            <Checkbox checked={editor.isRequired} onChange={(event) => setEditor({ ...editor, isRequired: event.target.checked })} />
            <span className="grid gap-0.5">{t('customFields.settings.isRequired')}<span className="text-muted-foreground text-xs">{t('customFields.settings.isRequiredHint')}</span></span>
          </label>
          {isOptionType(editor.type) && (
            <fieldset className="grid gap-2 sm:col-span-2">
              <legend className="text-muted-foreground mb-1 text-[12.5px] font-[550]">{t('customFields.settings.options')}</legend>
              {editor.options.map((option, index) => (
                <div key={index} className="flex flex-wrap items-center gap-2">
                  <Input aria-label={t('customFields.settings.optionLabel')} value={option.label} disabled={option.isDeprecated} className="min-w-40 flex-1"
                    onChange={(event) => setOption(index, { label: event.target.value })} />
                  <span className="text-muted-foreground w-36 truncate text-xs">{option.key || keyFromLabel(option.label)}</span>
                  {option.saved
                    ? <Button type="button" variant="ghost" size="sm" onClick={() => setOption(index, { isDeprecated: !option.isDeprecated })}>
                      {option.isDeprecated ? <><Undo2 aria-hidden strokeWidth={1.7} />{t('customFields.settings.optionRestore')}</> : t('customFields.settings.optionDeprecate')}
                    </Button>
                    : <Button type="button" variant="ghost" size="icon-sm" aria-label={t('customFields.settings.optionRemove')}
                      onClick={() => setEditor({ ...editor, options: editor.options.filter((_, at) => at !== index) })}><X aria-hidden /></Button>}
                </div>
              ))}
              <div className="flex flex-wrap items-center gap-3">
                <Button type="button" variant="outline" size="sm" onClick={() => setEditor({ ...editor, options: [...editor.options, { key: '', label: '', isDeprecated: false, saved: false }] })}>
                  <Plus aria-hidden strokeWidth={1.7} />{t('customFields.settings.addOption')}
                </Button>
                <span className="text-muted-foreground text-xs">{t('customFields.settings.optionsHint')}</span>
              </div>
            </fieldset>
          )}
          <div className="flex gap-2 sm:col-span-2">
            <Button type="submit" disabled={mutation.isPending}>{t('customFields.save')}</Button>
            <Button type="button" variant="outline" onClick={() => setEditor(null)}>{t('customFields.cancel')}</Button>
          </div>
        </form>
      )}

      <AlertDialog open={deprecateTarget !== null} onOpenChange={(open) => { if (!open) setDeprecateTarget(null) }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('customFields.settings.deprecateTitle', { name: deprecateTarget?.label ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('customFields.settings.deprecateDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <p role="status" className="text-[13px] font-[550]">
            {impact.isPending ? t('customFields.settings.impactLoading')
              : impact.isError ? t('customFields.settings.impactError')
                : impactCount === 0 ? t('customFields.settings.impactNone') : t('customFields.settings.impact', { count: impactCount })}
          </p>
          {impact.isSuccess && impactViews.length > 0 && (
            <div role="status" className="grid gap-1 text-[13px]">
              <p className="font-[550]">{t('customFields.settings.impactViews', { count: impactViews.length })}</p>
              <ul className="text-muted-foreground list-disc pl-5">{impactViews.map((view) => <li key={view.id}>{view.name}</li>)}</ul>
              <p className="text-muted-foreground text-[12.5px]">{t('customFields.settings.impactViewsHint')}</p>
            </div>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel>{t('customFields.cancel')}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => {
              if (deprecateTarget) void send({ operation: 'deprecate', id: deprecateTarget.id, expectedRowVersion: deprecateTarget.rowVersion })
              setDeprecateTarget(null)
            }}>{t('customFields.settings.deprecate')}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  )
}
