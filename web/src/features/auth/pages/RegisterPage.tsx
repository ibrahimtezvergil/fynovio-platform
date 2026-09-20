import { Loader2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuthConfig } from '@/features/auth/api'
import { RegisterForm } from '@/features/auth/components/RegisterForm'
import { paths } from '@/routes/paths'

/**
 * Sign-up is a server-side switch, off by default. While it is off (or the server cannot say) this is the
 * invite-only screen — never a form that could only fail.
 */
export default function RegisterPage() {
  const { t } = useTranslation('auth')
  const config = useAuthConfig()

  if (config.isPending) {
    return (
      <Card>
        <CardContent className="py-6">
          <output className="text-muted-foreground flex items-center justify-center">
            <Loader2 aria-hidden className="size-4 animate-spin" />
          </output>
        </CardContent>
      </Card>
    )
  }

  if (config.data?.selfRegistrationEnabled) return <RegisterForm />

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('register.inviteOnlyTitle')}</CardTitle>
        <CardDescription>{t('register.inviteOnlyDescription')}</CardDescription>
      </CardHeader>
      <CardContent>
        <Link to={paths.login} className={buttonVariants({ variant: 'outline', size: 'lg', className: 'w-full' })}>
          {t('register.backToLogin')}
        </Link>
      </CardContent>
    </Card>
  )
}
