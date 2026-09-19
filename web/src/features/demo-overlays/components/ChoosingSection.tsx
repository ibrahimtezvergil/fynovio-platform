import { GitCompareArrows } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui/card'

interface Row {
  name: string
  use: string
  avoid: string
  /** Whether the layer blocks the page behind it. */
  modal: string
}

/**
 * The page's premise, in one table: six layers that all "open on top", told
 * apart by what they take away from the user — the page behind, the escape
 * routes, or nothing at all.
 */
export function ChoosingSection() {
  const { t } = useTranslation('demo-overlays')

  const ROWS: readonly Row[] = [
    {
      name: t('choosingSection.rows.dialog.name'),
      use: t('choosingSection.rows.dialog.use'),
      avoid: t('choosingSection.rows.dialog.avoid'),
      modal: t('choosingSection.rows.dialog.modal'),
    },
    {
      name: t('choosingSection.rows.alertDialog.name'),
      use: t('choosingSection.rows.alertDialog.use'),
      avoid: t('choosingSection.rows.alertDialog.avoid'),
      modal: t('choosingSection.rows.alertDialog.modal'),
    },
    {
      name: t('choosingSection.rows.sheet.name'),
      use: t('choosingSection.rows.sheet.use'),
      avoid: t('choosingSection.rows.sheet.avoid'),
      modal: t('choosingSection.rows.sheet.modal'),
    },
    {
      name: t('choosingSection.rows.drawer.name'),
      use: t('choosingSection.rows.drawer.use'),
      avoid: t('choosingSection.rows.drawer.avoid'),
      modal: t('choosingSection.rows.drawer.modal'),
    },
    {
      name: t('choosingSection.rows.popover.name'),
      use: t('choosingSection.rows.popover.use'),
      avoid: t('choosingSection.rows.popover.avoid'),
      modal: t('choosingSection.rows.popover.modal'),
    },
    {
      name: t('choosingSection.rows.tooltip.name'),
      use: t('choosingSection.rows.tooltip.use'),
      avoid: t('choosingSection.rows.tooltip.avoid'),
      modal: t('choosingSection.rows.tooltip.modal'),
    },
  ]

  return (
    <Card id="secim" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <div className="flex items-start gap-3">
        <span aria-hidden className="nx-icon-tile mt-0.5">
          <GitCompareArrows className="size-4" strokeWidth={1.75} />
        </span>
        <div className="flex min-w-0 flex-col gap-0.5">
          <h2 className="font-heading text-[17px] leading-tight font-[620] tracking-[-0.024em]">
            {t('choosingSection.heading')}
          </h2>
          <p className="text-muted-foreground text-[12.5px]">
            {t('choosingSection.description')}
          </p>
        </div>
      </div>

      <div className="-mx-6 overflow-x-auto px-6">
        <table className="w-full min-w-[720px] border-collapse text-[12.5px]">
          <thead>
            <tr className="border-b border-[var(--nx-hairline)]">
              <th className="nx-eyebrow py-2 pr-4 text-left">{t('choosingSection.columns.layer')}</th>
              <th className="nx-eyebrow py-2 pr-4 text-left">{t('choosingSection.columns.use')}</th>
              <th className="nx-eyebrow py-2 pr-4 text-left">{t('choosingSection.columns.avoid')}</th>
              <th className="nx-eyebrow py-2 text-left">{t('choosingSection.columns.modal')}</th>
            </tr>
          </thead>
          <tbody>
            {ROWS.map((row) => (
              <tr key={row.name} className="border-b border-[var(--nx-hairline-soft)] last:border-0">
                <td className="py-3 pr-4 align-top font-[590] whitespace-nowrap">{row.name}</td>
                <td className="text-muted-foreground py-3 pr-4 align-top leading-5">{row.use}</td>
                <td className="text-muted-foreground py-3 pr-4 align-top leading-5">{row.avoid}</td>
                <td className="text-muted-foreground py-3 align-top leading-5">{row.modal}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
