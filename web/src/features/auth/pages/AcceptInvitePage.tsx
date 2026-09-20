import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useInvitationPreview } from '@/features/auth/api'
import { AcceptInviteForm } from '@/features/auth/components/AcceptInviteForm'
import { LinkInvalidNotice } from '@/features/auth/components/LinkInvalidNotice'
import { useFragmentToken } from '@/features/auth/lib/useFragmentToken'
import type { ApiError } from '@/types'

/** Opened from the e-mailed link (`/accept-invite#token=…`). Reachable whatever the session state — the link is the credential. */
export default function AcceptInvitePage() {
  const { t } = useTranslation('auth')
  const token = useFragmentToken()
  const preview = useInvitationPreview(token)
  const [linkInvalid, setLinkInvalid] = useState(false)

  if (!token || linkInvalid) return <LinkInvalidNotice />

  if (preview.isPending) {
    return (
      <Card>
        <CardContent className="py-6">
          <output className="text-muted-foreground flex items-center justify-center gap-2 text-[13px]">
            <Loader2 aria-hidden className="size-4 animate-spin" />
            {t('acceptInvite.checking')}
          </output>
        </CardContent>
      </Card>
    )
  }

  if (preview.isError) {
    // A 400 is the server's uniform "invalid, expired or used"; anything else (network, 5xx, 429) is worth a retry.
    if ((preview.error as ApiError).status === 400) return <LinkInvalidNotice />
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t('acceptInvite.title')}</CardTitle>
          <CardDescription role="alert">{t('acceptInvite.unreachable')}</CardDescription>
        </CardHeader>
        <CardContent>
          <Button type="button" size="lg" className="w-full" onClick={() => void preview.refetch()}>
            {t('errors.retry')}
          </Button>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('acceptInvite.title')}</CardTitle>
        <CardDescription>
          {preview.data.accountHasCredential ? t('acceptInvite.existingAccountDescription') : t('acceptInvite.newAccountDescription')}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <AcceptInviteForm token={token} preview={preview.data} onLinkInvalid={() => setLinkInvalid(true)} />
      </CardContent>
    </Card>
  )
}
