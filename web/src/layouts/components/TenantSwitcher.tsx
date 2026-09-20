import { Check } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { selectTenant, useSessionStore } from '@/lib/auth'
import { cn } from '@/lib/utils'

/**
 * Switches the active tenant. The server validates the membership and mints a
 * new access token for it; nothing here builds or edits a tenant claim. Rendered
 * only for accounts with more than one membership.
 */
export function TenantSwitcher({ onSwitched }: { onSwitched?: () => void }) {
  const { t } = useTranslation('auth')
  const memberships = useSessionStore((s) => s.memberships)
  const activeTenantId = useSessionStore((s) => s.activeTenantId)
  const [pendingId, setPendingId] = useState<number | null>(null)

  if (memberships.length < 2) return null

  const switchTo = async (tenantId: number) => {
    if (pendingId !== null || tenantId === activeTenantId) return
    setPendingId(tenantId)
    try {
      await selectTenant(tenantId) // clears the query cache; the shell refetches for the new tenant
      onSwitched?.()
    } catch {
      toast.error(t('tenantSwitcher.failed'))
    } finally {
      setPendingId(null)
    }
  }

  return (
    <fieldset className="m-0 flex min-w-0 flex-col gap-0.5 border-0 p-1.5">
      <legend className="sr-only">{t('tenantSwitcher.label')}</legend>
      {memberships.map(({ tenantId }) => {
        const active = tenantId === activeTenantId
        return (
          <button
            key={tenantId}
            type="button"
            aria-current={active ? 'true' : undefined}
            disabled={pendingId !== null}
            onClick={() => void switchTo(tenantId)}
            className={cn(
              'flex h-10 w-full items-center gap-2.5 rounded-[var(--nx-r-ctl-lg)] px-2.5 text-left text-[13.5px] font-medium transition-colors duration-[250ms] ease-fluid hover:bg-[var(--nx-fill-hover)] enabled:cursor-pointer disabled:cursor-default',
              active && 'bg-[var(--nx-fill-hover)] font-[590]',
            )}
          >
            <span aria-hidden className="nx-avatar size-[26px] shrink-0 rounded-[var(--nx-r-tile)] text-[11px]">
              {tenantId}
            </span>
            <span className="flex-1 truncate">{t('tenantSelector.tenantLabel', { id: tenantId })}</span>
            {active && <Check aria-hidden className="size-4 shrink-0" strokeWidth={1.75} />}
          </button>
        )
      })}
    </fieldset>
  )
}
