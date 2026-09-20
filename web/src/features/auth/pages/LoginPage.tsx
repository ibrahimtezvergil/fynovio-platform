import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router-dom'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { LoginForm } from '@/features/auth/components/LoginForm'
import { PASSWORD_UPDATED_NOTICE } from '@/features/auth/components/ResetPasswordForm'
import { paths } from '@/routes/paths'

export default function LoginPage() {
  const { t } = useTranslation('auth')
  const notice = (useLocation().state as { notice?: string } | null)?.notice

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('loginPage.title')}</CardTitle>
        <CardDescription>{t('loginPage.description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {notice === PASSWORD_UPDATED_NOTICE && (
          <output className="block text-[12.5px] text-[var(--nx-pos)]">{t('loginPage.passwordUpdated')}</output>
        )}
        <LoginForm />
        <Link to={paths.forgotPassword} className="text-muted-foreground hover:text-foreground text-center text-[12.5px]">
          {t('loginPage.forgotPassword')}
        </Link>
      </CardContent>
    </Card>
  )
}
