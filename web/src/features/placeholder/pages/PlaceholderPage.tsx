import { Construction } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { Card } from '@/components/ui/card'

/**
 * Every nav destination resolves to something until its feature slice lands.
 * `titleKey` indexes `placeholder:titles` rather than taking a literal string,
 * so the label stays reactive to a language switch — the route table only
 * ever hands this a stable key, never rendered copy.
 */
export function PlaceholderPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation('placeholder')
  const title = t(`titles.${titleKey}`)
  return (
    <div className="flex flex-col gap-5">
      <PageHeader title={title} description={t('notConnected')} />
      <Card className="p-0">
        <EmptyState
          icon={Construction}
          title={t('inProgress', { title })}
          description={t('inProgressDescription')}
        />
      </Card>
    </div>
  )
}
