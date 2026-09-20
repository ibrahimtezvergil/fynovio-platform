import { useTranslation } from 'react-i18next'
import { apiMode } from '@/lib/apiMode'

/** DEV only: says which API this page talks to, so nobody mistakes a mock for the real backend (spec §20). */
export function ApiModeChip() {
  const { t } = useTranslation('opportunities')
  if (!import.meta.env.DEV) return null
  return (
    <span
      data-testid="api-mode"
      title={apiMode.mockingEnabled ? t('apiMode.mockedElsewhere') : t('apiMode.noMocks')}
      className="text-muted-foreground border-border/60 rounded-full border px-2.5 py-0.5 text-[11px] font-[550]"
    >
      {t('apiMode.real', { base: apiMode.baseUrl })}
    </span>
  )
}
