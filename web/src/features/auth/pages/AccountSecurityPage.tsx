import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ChangePasswordForm } from '@/features/auth/components/ChangePasswordForm'

export default function AccountSecurityPage() {
  const { t } = useTranslation('auth')
  return (
    <div className="flex max-w-xl flex-col gap-6">
      <PageHeader title={t('accountSecurity.title')} description={t('accountSecurity.description')} />
      <Card>
        <CardHeader>
          <CardTitle>{t('accountSecurity.changeTitle')}</CardTitle>
        </CardHeader>
        <CardContent>
          <ChangePasswordForm />
        </CardContent>
      </Card>
    </div>
  )
}
