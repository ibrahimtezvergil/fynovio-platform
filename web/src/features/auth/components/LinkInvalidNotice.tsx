import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { paths } from '@/routes/paths'

/**
 * One answer for a link that does not work — missing, malformed, expired, already used, revoked. It never says
 * which, because the server does not either.
 */
export function LinkInvalidNotice({ offerNewPasswordLink = false }: { offerNewPasswordLink?: boolean }) {
  const { t } = useTranslation('auth')
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('linkInvalid.title')}</CardTitle>
        <CardDescription role="alert">{t('linkInvalid.description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {offerNewPasswordLink && (
          <Link to={paths.forgotPassword} className={buttonVariants({ size: 'lg', className: 'w-full' })}>
            {t('linkInvalid.requestNew')}
          </Link>
        )}
        <Link to={paths.login} className={buttonVariants({ variant: 'outline', size: 'lg', className: 'w-full' })}>
          {t('linkInvalid.backToLogin')}
        </Link>
      </CardContent>
    </Card>
  )
}
