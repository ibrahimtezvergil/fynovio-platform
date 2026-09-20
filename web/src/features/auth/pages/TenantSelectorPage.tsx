import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useSelectTenant } from '@/features/auth/api'
import { sanitizeReturnUrl, useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'

export default function TenantSelectorPage() {
  const { t } = useTranslation('auth')
  const memberships = useSessionStore((s) => s.memberships)
  const select = useSelectTenant()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (memberships.length === 0) return <Navigate to={paths.noAccess} replace />

  const choose = async (tenantId: number) => {
    if (select.isPending) return
    setPendingId(tenantId)
    setError(null)
    try {
      await select.mutateAsync(tenantId)
      navigate(sanitizeReturnUrl(params.get('returnUrl')), { replace: true })
    } catch (failure) {
      setError((failure as ApiError).status === 403 ? t('tenantSelector.notPermitted') : t('tenantSelector.genericError'))
      setPendingId(null)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('tenantSelector.title')}</CardTitle>
        <CardDescription>{t('tenantSelector.description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {memberships.map(({ tenantId }) => (
          <Button
            key={tenantId}
            type="button"
            variant="outline"
            size="lg"
            className="w-full justify-between"
            disabled={select.isPending}
            onClick={() => void choose(tenantId)}
          >
            <span>{t('tenantSelector.tenantLabel', { id: tenantId })}</span>
            {pendingId === tenantId && <Loader2 aria-hidden className="size-4 animate-spin" />}
          </Button>
        ))}
        {error && (
          <p role="alert" className="text-destructive text-[12px]">
            {error}
          </p>
        )}
      </CardContent>
    </Card>
  )
}
