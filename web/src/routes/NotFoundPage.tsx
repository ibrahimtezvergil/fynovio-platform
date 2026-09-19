import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { paths } from '@/routes/paths'

export default function NotFoundPage() {
  const { t } = useTranslation('routes')
  return (
    <div className="bg-background relative isolate flex min-h-screen flex-col items-center justify-center gap-3 p-6 text-center">
      <div aria-hidden className="nx-ambient" />
      <p className="nx-eyebrow relative z-[1]">404</p>
      <h1 className="relative z-[1] text-[26px] font-[620] tracking-[-0.03em]">
        {t('notFound.title')}
      </h1>
      <p className="text-muted-foreground relative z-[1] max-w-sm text-[13px]">
        {t('notFound.description')}
      </p>
      <Link to={paths.dashboard} className={buttonVariants({ className: 'relative z-[1] mt-2' })}>
        {t('notFound.backToDashboard')}
      </Link>
    </div>
  )
}
