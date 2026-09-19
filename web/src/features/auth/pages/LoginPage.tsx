import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { LoginForm } from '@/features/auth/components/LoginForm'

export default function LoginPage() {
  const { t } = useTranslation('auth')
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('loginPage.title')}</CardTitle>
        <CardDescription>{t('loginPage.description')}</CardDescription>
      </CardHeader>
      <CardContent>
        <LoginForm />
      </CardContent>
    </Card>
  )
}
