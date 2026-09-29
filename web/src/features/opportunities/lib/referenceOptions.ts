import type { SelectOption } from '@/components/common/inputs/types'
import type { AssignablePrincipal, PartyReference } from '../schema'

export const partyOption = (party: PartyReference): SelectOption => ({
  value: String(party.id),
  label: party.displayName,
  // The customer number, phone and e-mail: what tells two customers with the same name apart.
  description: [`#${party.id}`, party.phone, party.email].filter(Boolean).join(' · '),
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
