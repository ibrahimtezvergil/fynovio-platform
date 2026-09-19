import { Check } from 'lucide-react'
import type { FormEventHandler } from 'react'
import { useFormContext } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import type { SettingsValues } from '@/features/settings/schema'

/** RHF marks nested groups as objects of booleans — the count is their leaves. */
function countDirty(fields: unknown): number {
  if (fields === true) return 1
  if (fields && typeof fields === 'object') {
    return Object.values(fields).reduce<number>((total, value) => total + countDirty(value), 0)
  }
  return 0
}

/**
 * Sticky commit bar, and the settings form's only `<form>` element. The count
 * is react-hook-form's own diff against the last saved copy, so both buttons
 * are dead exactly when there is nothing to commit.
 */
export function SaveBar({ onSubmit }: { onSubmit: FormEventHandler<HTMLFormElement> }) {
  const { t } = useTranslation('settings')
  const {
    reset,
    formState: { dirtyFields, isSubmitting },
  } = useFormContext<SettingsValues>()

  const changes = countDirty(dirtyFields)

  return (
    <form
      onSubmit={onSubmit}
      className="nx-material sticky bottom-4 z-[4] flex flex-wrap items-center gap-3 rounded-[var(--nx-r-card)] px-5 py-3.5"
    >
      <span aria-live="polite" className="text-muted-foreground flex-1 text-[12.5px]">
        {changes === 0 ? t('saveBar.allSaved') : t('saveBar.unsavedCount', { count: changes })}
      </span>

      <Button type="button" variant="ghost" disabled={changes === 0} onClick={() => reset()}>
        {t('saveBar.discard')}
      </Button>
      <Button type="submit" disabled={changes === 0 || isSubmitting}>
        <Check aria-hidden strokeWidth={2} />
        {t('saveBar.save')}
      </Button>
    </form>
  )
}
