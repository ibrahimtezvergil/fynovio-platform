import { Send } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Button } from '@/components/ui/button'
import { useActivityMeta, type ActivityType, type TimelineEntry } from '@/features/demo-timeline/types'

/** The four kinds a person logs by hand. Everything else the system writes. */
const LOGGABLE = ['note', 'call', 'email', 'meeting'] as const satisfies readonly ActivityType[]

/**
 * Logging a touchpoint, at the head of the feed rather than behind a modal.
 *
 * A feed that can only be read gets stale, and the fix is never a "new
 * activity" dialog three clicks away: the composer sits where the result will
 * appear, and the entry it writes is the same shape as every other row.
 */
export function ActivityComposer({ onSubmit }: { onSubmit: (entry: TimelineEntry) => void }) {
  const { t } = useTranslation('demo-timeline')
  const activityMeta = useActivityMeta()
  const segments: readonly Segment<(typeof LOGGABLE)[number]>[] = useMemo(
    () => LOGGABLE.map((value) => ({ value, label: activityMeta[value].label })),
    [activityMeta],
  )
  const [type, setType] = useState<(typeof LOGGABLE)[number]>('note')
  const [text, setText] = useState('')

  const submit = () => {
    const trimmed = text.trim()
    if (!trimmed) return
    onSubmit({
      id: `local-${Date.now()}`,
      type,
      actor: 'Deniz Kaya',
      title: trimmed.split('\n')[0]!.slice(0, 90),
      detail: trimmed.length > 90 ? trimmed : undefined,
      at: new Date().toISOString(),
    })
    setText('')
  }

  return (
    <div className="flex flex-col gap-2.5 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-3">
      <SegmentedControl
        aria-label={t('composer.segmentAriaLabel')}
        segments={segments}
        value={type}
        onChange={setType}
      />
      <textarea
        value={text}
        onChange={(event) => setText(event.target.value)}
        rows={2}
        aria-label={t('composer.textareaAriaLabel')}
        placeholder={t('composer.placeholder')}
        className="w-full resize-none rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-surface)] px-3 py-2 text-[13px] outline-none transition-[border-color,box-shadow] duration-[250ms] ease-fluid placeholder:text-[var(--nx-label-3)] focus-visible:border-ring focus-visible:shadow-[0_0_0_4px_var(--nx-tint-fill)]"
      />
      <div className="flex items-center gap-2">
        <p className="text-[var(--nx-label-3)] flex-1 text-[11px]">{t('composer.helper')}</p>
        <Button size="sm" disabled={text.trim().length === 0} onClick={submit}>
          <Send strokeWidth={1.75} />
          {t('composer.submit')}
        </Button>
      </div>
    </div>
  )
}
