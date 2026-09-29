import { Building2, User } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { PartyReference } from '../schema'

/** The chosen customer, in full — the number, phone and e-mail are what tell two customers with the same name apart. */
export function SelectedParty({ party }: { party: PartyReference }) {
  const { t } = useTranslation('opportunities')
  const Icon = party.partyType === 'Person' ? User : Building2
  const facts: readonly [string, string | null | undefined][] = [
    [t('form.customerCard.number'), `#${party.id}`],
    [t('form.customerCard.phone'), party.phone],
    [t('form.customerCard.email'), party.email],
  ]
  return (
    <div className="bg-muted/30 flex gap-3 rounded-[var(--nx-r-ctl)] border px-4 py-3" data-testid="selected-party">
      <span aria-hidden className="bg-background text-muted-foreground grid size-9 shrink-0 place-items-center rounded-full border">
        <Icon className="size-4" strokeWidth={1.7} />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[14px] font-[590]">{party.displayName}</p>
        <p className="text-muted-foreground text-[12px]">{t(`form.customerCard.type.${party.partyType === 'Person' ? 'Person' : 'Organization'}`)}</p>
        <dl className="mt-2 grid gap-x-6 gap-y-1 text-[13px] sm:grid-cols-2">
          {facts.map(([label, value]) => (
            <div key={label} className="flex min-w-0 gap-2">
              <dt className="text-muted-foreground shrink-0">{label}</dt>
              <dd className="min-w-0 truncate">{value || <span className="text-muted-foreground">—</span>}</dd>
            </div>
          ))}
        </dl>
      </div>
    </div>
  )
}
