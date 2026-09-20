import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import type { SelectOption } from '@/components/common/inputs/types'
import { useReassignOpportunity } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import type { Opportunity } from '../schema'
import { parseAssignee } from '../lib/referenceOptions'
import { AssigneePicker } from './AssigneePicker'
import { CommandDialog } from './CommandDialog'

interface ReassignDialogProps {
  opportunity: Opportunity
  onClose: () => void
  onReload: () => void
}

/**
 * Reassign: the candidates come from the server's Assignable Principals query for THIS opportunity, and the server
 * re-validates the chosen one on submit. A 422 (the person stopped being assignable between list and submit)
 * clears the selection, which makes the picker re-ask the server for candidates — the UI never decides who is eligible.
 */
export function ReassignDialog({ opportunity, onClose, onReload }: ReassignDialogProps) {
  const { t } = useTranslation('opportunities')
  const [selected, setSelected] = useState<SelectOption | null>(null)
  const [missing, setMissing] = useState(false)
  const command = useKeyedCommand(useReassignOpportunity(), {
    onFailure: (problem) => {
      if (problem.kind !== 'notAssignable') return
      // Clearing the choice is what makes the picker ask the server for candidates again.
      setSelected(null)
    },
  })

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!selected) {
      setMissing(true)
      return
    }
    const { issuer, subject } = parseAssignee(selected)
    const result = await command.run({
      id: opportunity.id,
      expectedVersion: opportunity.rowVersion,
      newPrincipalIssuer: issuer,
      newPrincipalSubject: subject,
    })
    if (result) onClose()
  }

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('reassign.title')}
      description={t('reassign.description')}
      submitLabel={command.isPending ? t('reassign.submitting') : t('reassign.submit')}
      pending={command.isPending}
      problem={command.problem}
      onReload={onReload}
      onSubmit={submit}
    >
      <Field label={t('reassign.assignee.label')} error={missing && !selected ? t('reassign.assignee.required') : undefined}>
        {(props) => (
          <AssigneePicker
            {...props}
            opportunityId={opportunity.id}
            value={selected}
            onValueChange={(option) => {
              setSelected(option)
              command.reset()
              if (option) setMissing(false)
            }}
          />
        )}
      </Field>
    </CommandDialog>
  )
}
