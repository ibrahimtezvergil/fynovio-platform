import type { SelectOption } from '@/components/common/inputs/types'
import type { AssignablePrincipal, PartyReference } from '../schema'

export const partyOption = (party: PartyReference): SelectOption => ({
  value: String(party.id),
  label: party.displayName,
  description: party.email ?? party.partyType,
})

/** (issuer, subject) is the identity; encoded as JSON so no separator can be forged by a subject value. */
export const assigneeOption = (principal: AssignablePrincipal): SelectOption => ({
  value: JSON.stringify([principal.issuer, principal.subject]),
  label: principal.displayName,
  description: principal.email ?? undefined,
})

export function parseAssignee(option: SelectOption): { issuer: string; subject: string } {
  const [issuer, subject] = JSON.parse(option.value) as [string, string]
  return { issuer, subject }
}
