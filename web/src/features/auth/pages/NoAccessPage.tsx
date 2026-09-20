import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { logout, useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'

/**
 * The 403 experience: says that access is missing, never what is hidden. Also
 * where a signed-in account with no tenant membership lands. It does not end
 * the session — 403 means "signed in but not permitted", unlike 401.
 */
export default function NoAccessPage() {
  const { t } = useTranslation('auth')
  const noMembership = useSessionStore((s) => s.noMembership)
  const status = useSessionStore((s) => s.status)
  const navigate = useNavigate()

  const signOut = async () => {
    await logout()
    navigate(paths.login, { replace: true })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('noAccess.title')}</CardTitle>
        <CardDescription>
          {noMembership ? t('noAccess.noMembershipDescription') : t('noAccess.forbiddenDescription')}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {status === 'authenticated' && (
          <Link to={paths.dashboard} className={buttonVariants({ variant: 'outline', size: 'lg', className: 'w-full' })}>
            {t('noAccess.backToDashboard')}
          </Link>
        )}
        <Button type="button" variant="outline" size="lg" className="w-full" onClick={() => void signOut()}>
          {t('noAccess.signOut')}
        </Button>
      </CardContent>
    </Card>
  )
}
