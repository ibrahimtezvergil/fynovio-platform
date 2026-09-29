import { UserPlus } from 'lucide-react'
import { useRef } from 'react'
import { useTranslation } from 'react-i18next'
import type { FieldControlProps, SelectOption } from '@/components/common/inputs/types'
import { usePartySearcher } from '../api'
import { partyOption } from '../lib/referenceOptions'
import type { PartyReference } from '../schema'
import { ReferenceSearchField } from './ReferenceSearchField'

interface PartyPickerProps extends FieldControlProps {
  value: SelectOption | null
  onValueChange: (value: SelectOption | null) => void
  /** The full record behind the chosen option (phone, e-mail, number) — the option itself only carries a label. */
  onPartyChange?: (party: PartyReference | null) => void
  /** Offered under every search: create the customer that was just typed. Receives the typed text. */
  onCreate?: (query: string) => void
}

/**
 * Choose the customer of a new opportunity from the tenant's Parties. The value is the Party id the backend will verify.
 * The search matches name, phone, e-mail and customer number; each hit shows name, number, phone and e-mail.
 */
export function PartyPicker({ value, onValueChange, onPartyChange, onCreate, ...aria }: PartyPickerProps) {
  const { t } = useTranslation('opportunities')
  const searchParties = usePartySearcher()
  const seen = useRef(new Map<string, PartyReference>())
  return (
    <ReferenceSearchField
      {...aria}
      value={value}
      onValueChange={(option) => {
        onValueChange(option)
        onPartyChange?.(option ? (seen.current.get(option.value) ?? null) : null)
      }}
      search={async (query) => {
        const parties = await searchParties(query)
        for (const party of parties) seen.current.set(String(party.id), party)
        return parties.map(partyOption)
      }}
      placeholder={t('form.partyId.placeholder')}
      emptyMessage={t('picker.empty')}
      errorMessage={t('picker.error')}
      forbiddenMessage={t('picker.forbidden')}
      footer={
        onCreate
          ? ({ query, close }) => (
              <button
                type="button"
                className="hover:bg-accent focus-visible:bg-accent flex w-full cursor-pointer items-center gap-2 px-3 py-2.5 text-left text-[13px] font-[550] text-[var(--nx-tint)] outline-none"
                onClick={() => {
                  close()
                  onCreate(query)
                }}
              >
                <UserPlus aria-hidden className="size-4 shrink-0" strokeWidth={1.8} />
                {query ? t('form.partyId.createNamed', { query }) : t('form.newPartyAction')}
              </button>
            )
          : undefined
      }
    />
  )
}
