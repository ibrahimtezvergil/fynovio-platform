import { ArrowDown, ArrowUp, MoreHorizontal, Pencil, Plus, Power, PowerOff, X } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Alert, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { useCustomFieldDefinitions } from '@/lib/custom-fields/api'
import { activeFields, keyFromLabel } from '@/lib/custom-fields/values'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import { useManageSharedView, useSharedViews, type ManageSharedViewVariables } from '@/lib/shared-views/api'
import { BUILT_IN_VIEW_COLUMNS, MAX_VIEW_COLUMNS, type SharedView, type ViewColumn } from '@/lib/shared-views/schema'
import type { ApiError } from '@/types'
import { SectionHeading } from './SectionHeading'

type WithoutKey<T> = T extends unknown ? Omit<T, 'idempotencyKey'> : never
interface EditorState { id: number | null; rowVersion: number; key: string; name: string; sortOrder: string; columns: ViewColumn[] }

const emptyEditor = (sortOrder: number): EditorState => ({ id: null, rowVersion: 0, key: '', name: '', sortOrder: String(sortOrder), columns: [] })
const toEditor = (view: SharedView): EditorState => ({ id: view.id, rowVersion: view.rowVersion, key: view.key, name: view.name, sortOrder: String(view.sortOrder), columns: view.columns })
const sameColumn = (a: ViewColumn, b: ViewColumn) => a.kind === b.kind && a.key === b.key
const encode = (column: ViewColumn) => `${column.kind}:${column.key}`
const decode = (value: string): ViewColumn => { const [kind, ...rest] = value.split(':'); return { kind: kind as ViewColumn['kind'], key: rest.join(':') } }

/**
 * Tenant-shared table views for the opportunity list: an ordered list of columns, built-in or tenant fields. A view's key is
 * fixed once created; every change is published as a change set on the server, and a field the view uses can still be deprecated
 * later (the view then simply shows the columns that remain).
 */
export function SharedViewsPanel() {
  const { t } = useTranslation('opportunities')
  const views = useSharedViews()
  const definitions = useCustomFieldDefinitions()
  const mutation = useManageSharedView()
  const keys = useAttemptKeys()
  const [editor, setEditor] = useState<EditorState | null>(null)
  const [error, setError] = useState('')
  const items = views.data ?? []
  const fields = useMemo(() => activeFields(definitions.data ?? []), [definitions.data])
  const fieldLabel = (key: string) => definitions.data?.find((definition) => definition.fieldName === key)

  const columnLabel = (column: ViewColumn) => {
    if (column.kind === 'builtin') return t(`views.builtIn.${column.key}`)
    const definition = fieldLabel(column.key)
    return definition ? definition.label : column.key
  }
  const columnRetired = (column: ViewColumn) => column.kind === 'field' && fieldLabel(column.key)?.status !== 'Active'

  const failure = (problem: unknown) => {
    const apiError = problem as ApiError
    keys.settle(apiError)
    setError(apiError.code === 'view_key_conflict' ? t('views.settings.keyConflict')
      : apiError.code === 'view_limit_exceeded' ? t('views.settings.limit')
        : apiError.code === 'view_column_unknown' ? t('views.settings.columnUnknown')
          : apiError.code === 'view_column_deprecated' ? t('views.settings.columnDeprecated')
            : apiError.code === 'concurrency_conflict' ? t('views.settings.conflict')
              : t('views.settings.saveError'))
  }
  const send = async (variables: WithoutKey<ManageSharedViewVariables>) => {
    try {
      await mutation.mutateAsync({ ...variables, idempotencyKey: keys.begin(variables) } as ManageSharedViewVariables)
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
    const common = { name: editor.name.trim(), sortOrder: Number(editor.sortOrder) || 0, columns: editor.columns }
    const saved = editor.id === null
      ? await send({ operation: 'create', key: editor.key.trim() || keyFromLabel(editor.name), ...common })
      : await send({ operation: 'update', id: editor.id, expectedRowVersion: editor.rowVersion, ...common })
    if (saved) setEditor(null)
  }

  const move = (index: number, by: -1 | 1) =>
    setEditor((current) => {
      if (!current) return current
      const target = index + by
      if (target < 0 || target >= current.columns.length) return current
      const columns = [...current.columns];
      [columns[index], columns[target]] = [columns[target], columns[index]]
      return { ...current, columns }
    })

  const addable = editor === null ? [] : [
    ...BUILT_IN_VIEW_COLUMNS.map((key): ViewColumn => ({ kind: 'builtin', key })),
    ...fields.map((definition): ViewColumn => ({ kind: 'field', key: definition.fieldName })),
  ].filter((candidate) => !editor.columns.some((column) => sameColumn(column, candidate)))
  const atLimit = (editor?.columns.length ?? 0) >= MAX_VIEW_COLUMNS

  return (
    <Card className="min-w-0 gap-4 px-6 pt-[22px] pb-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <SectionHeading title={t('views.settings.title')} description={t('views.settings.description')} />
        <Button type="button" variant="outline" onClick={() => { setError(''); setEditor(emptyEditor((items.at(-1)?.sortOrder ?? 0) + 10)) }}>
          <Plus aria-hidden strokeWidth={1.7} />{t('views.settings.add')}
        </Button>
      </div>
      {error && <Alert variant="destructive"><AlertTitle>{error}</AlertTitle></Alert>}

      {views.isPending && <Skeleton className="h-24" />}
      {views.isSuccess && items.length === 0 && editor === null && (
        <p className="text-muted-foreground rounded-[var(--nx-r-ctl)] border border-dashed px-4 py-8 text-center text-sm">{t('views.settings.empty')}</p>
      )}

      {items.length > 0 && (
        <ul className="divide-y rounded-[var(--nx-r-card)] border">
          {items.map((item) => {
            const deprecated = item.status === 'Deprecated'
            return (
              <li key={item.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5">
                <div className={`min-w-0 flex-1 ${deprecated ? 'text-muted-foreground' : ''}`}>
                  <p className="truncate font-medium">{item.name}</p>
                  <p className="text-muted-foreground truncate text-xs">{[item.key, t('views.settings.columnCount', { count: item.columns.length })].join(' · ')}</p>
                </div>
                <Badge variant={deprecated ? 'secondary' : 'success'}>{deprecated ? t('views.settings.deprecated') : t('views.settings.active')}</Badge>
                {!deprecated && (
                  <Button type="button" variant="outline" size="sm" disabled={mutation.isPending} onClick={() => { setError(''); setEditor(toEditor(item)) }}>
                    <Pencil aria-hidden strokeWidth={1.7} />{t('views.settings.edit')}
                  </Button>
                )}
                <DropdownMenu>
                  <DropdownMenuTrigger render={<Button type="button" variant="ghost" size="icon-sm" disabled={mutation.isPending} aria-label={t('views.settings.more', { name: item.name })}><MoreHorizontal aria-hidden /></Button>} />
                  <DropdownMenuContent align="end" className="w-56">
                    {deprecated
                      ? <DropdownMenuItem onClick={() => void send({ operation: 'reactivate', id: item.id, expectedRowVersion: item.rowVersion })}><Power aria-hidden strokeWidth={1.7} />{t('views.settings.reactivate')}</DropdownMenuItem>
                      : <DropdownMenuItem variant="destructive" onClick={() => void send({ operation: 'deprecate', id: item.id, expectedRowVersion: item.rowVersion })}><PowerOff aria-hidden strokeWidth={1.7} />{t('views.settings.deprecate')}</DropdownMenuItem>}
                  </DropdownMenuContent>
                </DropdownMenu>
              </li>
            )
          })}
        </ul>
      )}

      {editor !== null && (
        <form onSubmit={(event) => void save(event)} className="grid gap-3 border-t pt-4 sm:grid-cols-2" aria-label={editor.id === null ? t('views.settings.add') : t('views.settings.edit')}>
          <Field label={t('views.settings.name')}>
            {(props) => <Input {...props} value={editor.name} required maxLength={100} autoComplete="off" onChange={(event) => setEditor({ ...editor, name: event.target.value })} />}
          </Field>
          <Field label={t('views.settings.key')} hint={t('views.settings.keyHint')}>
            {(props) => <Input {...props} value={editor.id === null ? editor.key || keyFromLabel(editor.name) : editor.key} disabled={editor.id !== null} maxLength={63} autoComplete="off"
              onChange={(event) => setEditor({ ...editor, key: event.target.value })} />}
          </Field>
          <Field label={t('views.settings.sortOrder')}>
            {(props) => <Input {...props} type="number" min="0" max="10000" value={editor.sortOrder} onChange={(event) => setEditor({ ...editor, sortOrder: event.target.value })} />}
          </Field>

          <fieldset className="grid gap-2 sm:col-span-2">
            <legend className="text-muted-foreground mb-1 text-[12.5px] font-[550]">{t('views.settings.columns')}</legend>
            {editor.columns.length === 0 && <p className="text-muted-foreground text-[13px]">{t('views.settings.columnsEmpty')}</p>}
            <ol className="grid gap-1.5">
              {editor.columns.map((column, index) => (
                <li key={encode(column)} className="flex flex-wrap items-center gap-2 rounded-[var(--nx-r-ctl)] border px-3 py-1.5 text-[13.5px]">
                  <span className="min-w-0 flex-1 truncate">{columnLabel(column)}</span>
                  {column.kind === 'field' && <Badge variant="secondary">{columnRetired(column) ? t('views.settings.fieldRetired') : t('views.settings.fieldColumn')}</Badge>}
                  <Button type="button" variant="ghost" size="icon-sm" disabled={index === 0} aria-label={t('views.settings.moveUp', { name: columnLabel(column) })} onClick={() => move(index, -1)}><ArrowUp aria-hidden /></Button>
                  <Button type="button" variant="ghost" size="icon-sm" disabled={index === editor.columns.length - 1} aria-label={t('views.settings.moveDown', { name: columnLabel(column) })} onClick={() => move(index, 1)}><ArrowDown aria-hidden /></Button>
                  <Button type="button" variant="ghost" size="icon-sm" aria-label={t('views.settings.removeColumn', { name: columnLabel(column) })}
                    onClick={() => setEditor({ ...editor, columns: editor.columns.filter((other) => !sameColumn(other, column)) })}><X aria-hidden /></Button>
                </li>
              ))}
            </ol>
            <Field label={t('views.settings.addColumn')} hint={atLimit ? t('views.settings.columnLimit', { count: MAX_VIEW_COLUMNS }) : undefined}>
              {(props) => (
                <Select {...props} value="" disabled={atLimit || addable.length === 0}
                  onChange={(event) => event.target.value && setEditor({ ...editor, columns: [...editor.columns, decode(event.target.value)] })}>
                  <option value="">{t('views.settings.chooseColumn')}</option>
                  <optgroup label={t('views.settings.builtInGroup')}>
                    {addable.filter((column) => column.kind === 'builtin').map((column) => <option key={encode(column)} value={encode(column)}>{columnLabel(column)}</option>)}
                  </optgroup>
                  {addable.some((column) => column.kind === 'field') && (
                    <optgroup label={t('views.settings.fieldsGroup')}>
                      {addable.filter((column) => column.kind === 'field').map((column) => <option key={encode(column)} value={encode(column)}>{columnLabel(column)}</option>)}
                    </optgroup>
                  )}
                </Select>
              )}
            </Field>
          </fieldset>

          <div className="flex gap-2 sm:col-span-2">
            <Button type="submit" disabled={mutation.isPending || editor.columns.length === 0}>{t('customFields.save')}</Button>
            <Button type="button" variant="outline" onClick={() => setEditor(null)}>{t('customFields.cancel')}</Button>
          </div>
        </form>
      )}
    </Card>
  )
}
