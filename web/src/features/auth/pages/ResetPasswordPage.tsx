import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { LinkInvalidNotice } from '@/features/auth/components/LinkInvalidNotice'
import { ResetPasswordForm } from '@/features/auth/components/ResetPasswordForm'
import { useFragmentToken } from '@/features/auth/lib/useFragmentToken'

/** Opened from the e-mailed link (`/reset-password#token=…`). Reachable whatever the session state — the link is the credential. */
export default function ResetPasswordPage() {
  const { t } = useTranslation('auth')
  const token = useFragmentToken()
  const [linkInvalid, setLinkInvalid] = useState(false)

  if (!token || linkInvalid) return <LinkInvalidNotice offerNewPasswordLink />

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('resetPassword.title')}</CardTitle>
        <CardDescription>{t('resetPassword.description')}</CardDescription>
      </CardHeader>
      <CardContent>
        <ResetPasswordForm token={token} onLinkInvalid={() => setLinkInvalid(true)} />
      </CardContent>
    </Card>
  )
}
