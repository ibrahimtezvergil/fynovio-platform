import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { i18n } from '@/lib/i18n'

const WORKSPACE = 'Nordwind Lojistik'

/**
 * The gate is the schema: nothing but the exact workspace name validates.
 * The message resolves at module-load time via the shared `i18n` instance
 * (a schema is a plain object, not a component) — see `stageMeta()` in
 * `StageBadge.tsx` for the same accepted tradeoff elsewhere.
 */
const confirmSchema = z.object({
  confirm: z
    .string()
    .trim()
    .refine((value) => value === WORKSPACE, i18n.t('dangerZone.nameMismatch', { ns: 'settings' })),
})

/** The refinement narrows `confirm` to the literal, so input and output differ. */
type ConfirmInput = z.input<typeof confirmSchema>
type ConfirmOutput = z.output<typeof confirmSchema>

/**
 * Deleting a workspace is irreversible, so it is gated behind typing the
 * workspace name — the artboard shows the single button, but a bare click is
 * not an acceptable confirmation for this action.
 */
export function DangerZone() {
  const { t } = useTranslation('settings')
  // Disclosure only; the typed name itself is form state.
  const [confirming, setConfirming] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isValid },
  } = useForm<ConfirmInput, unknown, ConfirmOutput>({
    resolver: zodResolver(confirmSchema),
    defaultValues: { confirm: '' },
    mode: 'onChange',
  })

  const onSubmit = handleSubmit(() => {
    toast.error(t('dangerZone.noBackendYet'))
  })

  const cancel = () => {
    setConfirming(false)
    reset()
  }

  return (
    <Card
      id="tehlikeli-bolge"
      className="scroll-mt-24 gap-4 border-[var(--nx-st-red-bg)] px-6 py-[18px]"
    >
      <div className="flex flex-wrap items-center gap-4">
        <span className="flex min-w-0 flex-1 flex-col gap-0.5">
          <span className="text-[14px] font-[590]">{t('dangerZone.title')}</span>
          <span className="text-muted-foreground text-[12.5px]">
            {t('dangerZone.description', { workspace: WORKSPACE })}
          </span>
        </span>

        {!confirming && (
          <Button variant="destructive" onClick={() => setConfirming(true)}>
            {t('dangerZone.title')}
          </Button>
        )}
      </div>

      {confirming && (
        <form
          onSubmit={onSubmit}
          noValidate
          className="flex flex-wrap items-end gap-3 border-t border-[var(--nx-hairline)] pt-4"
        >
          <label className="flex min-w-[240px] flex-1 flex-col gap-1.5">
            <span className="text-muted-foreground text-[12.5px] font-[550]">
              {t('dangerZone.typeToConfirm', { workspace: WORKSPACE })}
            </span>
            <Input
              placeholder={WORKSPACE}
              aria-invalid={errors.confirm ? true : undefined}
              {...register('confirm')}
            />
          </label>
          <Button type="submit" variant="destructive" disabled={!isValid}>
            {t('dangerZone.deletePermanently')}
          </Button>
          <Button type="button" variant="ghost" onClick={cancel}>
            {t('dangerZone.cancel')}
          </Button>
        </form>
      )}
    </Card>
  )
}
