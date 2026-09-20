import { useTranslation } from 'react-i18next'
import { useSessionStore } from '@/lib/auth'

/** Before noon reads "Günaydın", after reads "İyi günler" — the two forms the brief itself gives. */
function useGreetingKey(): 'morning' | 'day' {
  return new Date().getHours() < 12 ? 'morning' : 'day'
}

interface GreetingSectionProps {
  attentionCount: number
}

/** Calm, first-person context — not a marketing hero. Sets the day's one question: what needs me now. */
export function GreetingSection({ attentionCount }: GreetingSectionProps) {
  const { t } = useTranslation('home')
  const user = useSessionStore((s) => s.user)
  const greetingKey = useGreetingKey()
  const firstName = user?.name.split(' ')[0] ?? t('greeting.fallbackName')

  return (
    <div className="shrink-0">
      <h1 className="text-[22px] leading-[1.15] font-[620] tracking-[-0.03em]">
        {t(`greeting.${greetingKey}`, { name: firstName })}
      </h1>
      <p className="text-muted-foreground mt-1 text-[13px] leading-5">
        {attentionCount > 0
          ? t('greeting.subtitleWithCount', { count: attentionCount })
          : t('greeting.subtitleClear')}
      </p>
    </div>
  )
}
