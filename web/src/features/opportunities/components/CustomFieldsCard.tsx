import { Pencil } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { CustomFieldInputs } from '@/components/custom-fields/CustomFieldInput'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useCustomFieldDefinitions } from '@/lib/custom-fields/api'
import { customFieldErrors, requiredErrors } from '@/lib/custom-fields/errors'
import { activeFields, formatCustomFieldValue, hasValue, missingRequired, toDrafts, toPayload, type CustomFieldDrafts } from '@/lib/custom-fields/values'
import { useUpdateOpportunityCustomFields } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import type { Opportunity } from '../schema'
import { ProblemNotice } from './ProblemNotice'

/**
 * The opportunity's tenant-defined fields, rendered from the definitions. Deprecated fields that still hold a value
 * stay visible read-only (ADR decision 7) and are never sent back — the server carries them forward on save.
 */
export function CustomFieldsCard({ opportunity, onSaved }: { opportunity: Opportunity; onSaved: () => void }) {
  const { t, i18n } = useTranslation('opportunities')
  const definitions = useCustomFieldDefinitions()
  const mutation = useUpdateOpportunityCustomFields()
  const command = useKeyedCommand(mutation)
  const [drafts, setDrafts] = useState<CustomFieldDrafts | null>(null)
  const [missing, setMissing] = useState<string[]>([])
  const all = useMemo(() => definitions.data ?? [], [definitions.data])
  const fields = useMemo(() => activeFields(all), [all])
  const values = opportunity.customFields ?? {}
  const retired = all.filter((definition) => definition.status === 'Deprecated' && hasValue(values[definition.fieldName]))
  const errors = useMemo(() => ({ ...customFieldErrors(t, mutation.error), ...requiredErrors(t, missing) }), [t, mutation.error, missing])
  const yesNo = { yes: t('customFields.yes'), no: t('customFields.no') }

  if (fields.length === 0 && retired.length === 0) return null

  const editing = drafts !== null
  const canEdit = !opportunity.isArchived && fields.length > 0
  const begin = () => { mutation.reset(); setMissing([]); setDrafts(toDrafts(fields, values)) }
  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!drafts) return
    const required = missingRequired(fields, drafts)
    setMissing(required)
    if (required.length > 0) return
    const result = await command.run({ id: opportunity.id, expectedVersion: opportunity.rowVersion, customFields: toPayload(fields, drafts) })
    if (result) { setDrafts(null); onSaved() }
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3">
        <div className="grid gap-1">
          <CardTitle>{t('customFields.title')}</CardTitle>
          {editing && <CardDescription>{t('customFields.formDescription')}</CardDescription>}
        </div>
        {canEdit && !editing && (
          <Button type="button" variant="outline" size="sm" onClick={begin}>
            <Pencil aria-hidden strokeWidth={1.7} />{t('customFields.edit')}
          </Button>
        )}
      </CardHeader>
      <CardContent className="grid gap-4">
        {editing ? (
          <form onSubmit={(event) => void save(event)} noValidate aria-label={t('customFields.title')} className="grid gap-4">
            <CustomFieldInputs definitions={fields} drafts={drafts} errors={errors} disabled={command.isPending}
              onChange={(next) => { setDrafts(next); setMissing([]) }} />
            {command.problem && command.problem.kind !== 'validation' && <ProblemNotice problem={command.problem} />}
            <div className="flex gap-2">
              <Button type="submit" disabled={command.isPending}>{command.isPending ? t('customFields.saving') : t('customFields.save')}</Button>
              <Button type="button" variant="outline" disabled={command.isPending} onClick={() => setDrafts(null)}>{t('customFields.cancel')}</Button>
            </div>
          </form>
        ) : (
          fields.length > 0 && (
            <dl className="divide-border/60 divide-y">
              {fields.map((definition) => (
                <div key={definition.id} className="grid grid-cols-[150px_1fr] gap-3 py-1.5 text-[13px]">
                  <dt className="text-muted-foreground">{definition.label}</dt>
                  <dd className="min-w-0 break-words">{formatCustomFieldValue(definition, values[definition.fieldName], yesNo, i18n.language) || '—'}</dd>
                </div>
              ))}
            </dl>
          )
        )}
        {retired.length > 0 && (
          <div className="grid gap-1.5">
            <p className="text-muted-foreground text-[12.5px]">{t('customFields.deprecatedHint')}</p>
            <dl className="divide-border/60 divide-y">
              {retired.map((definition) => (
                <div key={definition.id} className="text-muted-foreground grid grid-cols-[150px_1fr] gap-3 py-1.5 text-[13px]">
                  <dt className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">{definition.label}<Badge variant="secondary">{t('customFields.deprecated')}</Badge></dt>
                  <dd className="min-w-0 break-words">{formatCustomFieldValue(definition, values[definition.fieldName], yesNo, i18n.language)}</dd>
                </div>
              ))}
            </dl>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
