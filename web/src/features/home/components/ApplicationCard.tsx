import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { APPLICATION_ICON } from '@/lib/applications/icons'
import type { ApplicationItem } from '@/lib/applications/types'
import { cn } from '@/lib/utils'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

/** The launcher uses descriptive cards; the all-applications drawer uses tiles. */
export function ApplicationCard({ app, onNavigate, variant = 'tile' }: { app: ApplicationItem; onNavigate?: () => void; variant?: 'tile' | 'detail' }) {
  const { t } = useTranslation('home')
  const navigate = useNavigate()
  const Icon = APPLICATION_ICON[app.icon]
  const comingSoon = app.status === 'comingSoon'

  return (
    <Tooltip>
      <TooltipTrigger render={
    <button
      type="button"
      disabled={comingSoon}
      onClick={() => {
        onNavigate?.()
        if (app.route) navigate(app.route)
      }}
      className={cn(
        'nx-material group/app relative flex h-25 w-26 shrink-0 flex-col items-center justify-center gap-2 rounded-lg px-2 py-2 text-center transition-[background-color,border-color,transform] duration-200 ease-fluid hover:border-ring/40 hover:bg-muted focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/40 active:scale-[0.97] motion-reduce:transform-none motion-reduce:transition-none',
        variant === 'detail' && 'h-auto min-h-25 w-full min-w-0 flex-row justify-start gap-3 p-4 text-left',
        comingSoon && 'pointer-events-none opacity-40',
      )}
    >
      <span aria-hidden className="flex size-10 shrink-0 items-center justify-center rounded-md border border-primary/15 bg-accent text-accent-foreground transition-colors group-hover/app:border-primary/30 motion-reduce:transition-none">
        <Icon className="size-5" strokeWidth={1.7} />
      </span>
      {variant === 'detail' ? (
        <span className="min-w-0 flex-1">
          <span className="block pr-3 text-sm leading-5 font-semibold">{t(app.labelKey)}</span>
          <span className="mt-1 block text-xs leading-5 text-muted-foreground">{t(app.descriptionKey)}</span>
        </span>
      ) : (
        <span className="flex min-h-8 w-full items-center justify-center text-xs leading-4 font-semibold">{t(app.labelKey)}</span>
      )}
      {comingSoon ? (
        <span className="absolute top-1 right-1 rounded-sm bg-background px-1 text-[10px] font-semibold text-muted-foreground">{t('applications.comingSoon')}</span>
      ) : (
        app.badge != null && (
          <span
            aria-hidden
            className="tnum absolute top-2 right-2 flex h-5 min-w-5 items-center justify-center rounded-full border border-primary/20 bg-background px-1 text-[10px] leading-none font-semibold text-primary"
          >
            {app.badge > 99 ? '99+' : app.badge}
          </span>
        )
      )}
    </button>
      } />
      <TooltipContent side="top" sideOffset={8}>
        {t(app.descriptionKey)}
      </TooltipContent>
    </Tooltip>
  )
}
