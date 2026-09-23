import { Check, Plus, Settings2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { selectTenant, useSessionStore, useTenantSwitchGuard } from '@/lib/auth'
import { cn } from '@/lib/utils'
import { paths } from '@/routes/paths'

/**
 * Switches the active tenant. The server validates the membership and mints a
 * new access token for it; nothing here builds or edits a tenant claim. With one
 * membership it remains the entry point for company settings and creation.
 */
export function TenantSwitcher({ onSwitched }: { onSwitched?: () => void }) {
  const { t } = useTranslation('auth')
  const { t: tNav } = useTranslation('nav')
  const memberships = useSessionStore((s) => s.memberships)
  const activeTenantId = useSessionStore((s) => s.activeTenantId)
  const confirmTenantSwitch = useTenantSwitchGuard((s) => s.confirm)
  const [pendingId, setPendingId] = useState<number | null>(null)

  const switchTo = async (tenantId: number) => {
    if (pendingId !== null || tenantId === activeTenantId) return
    if (confirmTenantSwitch && !await confirmTenantSwitch()) return
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
      {memberships.map((membership) => {
        const { tenantId } = membership
        const active = tenantId === activeTenantId
        const label = membership.displayName
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
            <span className="flex-1 truncate">{label}</span>
            {active && <Check aria-hidden className="size-4 shrink-0" strokeWidth={1.75} />}
          </button>
        )
      })}
      <div className="my-1 border-t" />
      <Link to={paths.companySettings} className="nx-nav-item mx-0.5">
        <Settings2 aria-hidden className="size-[17px] shrink-0" strokeWidth={1.7} />
        <span>{tNav('tenantSwitcher.companySettings')}</span>
      </Link>
      <Link to={paths.companyCreate} className="nx-nav-item mx-0.5">
        <Plus aria-hidden className="size-[17px] shrink-0" strokeWidth={1.7} />
        <span>{tNav('tenantSwitcher.createCompany')}</span>
      </Link>
    </fieldset>
  )
}
