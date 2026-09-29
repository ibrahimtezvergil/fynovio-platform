import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '@/components/common/PageHeader'
import { paths } from '@/routes/paths'
import { OpportunityForm } from '../components/OpportunityForm'

export default function OpportunityNewPage() {
  const { t } = useTranslation('opportunities')
  const navigate = useNavigate()
  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        eyebrow={t('list.eyebrow')}
        title={t('form.title')}
        description={t('form.description')}
        onBack={() => navigate(paths.crmOpportunities)}
        backLabel={t('common.backToList')}
      />
      <OpportunityForm />
    </div>
  )
}
