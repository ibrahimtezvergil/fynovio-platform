import { GitCompareArrows } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui/card'

interface Row {
  name: string
  when: string
  shows: string
  action: string
}

/**
 * Seven states that all render "nothing", told apart by why there is nothing.
 * The distinction is not decorative: the wrong one sends the reader to create
 * a record when the record already exists behind a filter, or to retry a
 * request that never failed.
 */
export function ChoosingSection() {
  const { t } = useTranslation('demo-states')
  const rows = t('choosing.rows', { returnObjects: true }) as Row[]

  return (
    <Card id="secim" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <div className="flex items-start gap-3">
        <span aria-hidden className="nx-icon-tile mt-0.5">
          <GitCompareArrows className="size-4" strokeWidth={1.75} />
        </span>
        <div className="flex min-w-0 flex-col gap-0.5">
          <h2 className="font-heading text-[17px] leading-tight font-[620] tracking-[-0.024em]">
            {t('choosing.title')}
          </h2>
          <p className="text-muted-foreground text-[12.5px]">{t('choosing.description')}</p>
        </div>
      </div>

      <div className="-mx-6 overflow-x-auto px-6">
        <table className="w-full min-w-[760px] border-collapse text-[12.5px]">
          <thead>
            <tr className="border-b border-[var(--nx-hairline)]">
              <th className="nx-eyebrow py-2 pr-4 text-left">{t('choosing.headers.state')}</th>
              <th className="nx-eyebrow py-2 pr-4 text-left">{t('choosing.headers.when')}</th>
              <th className="nx-eyebrow py-2 pr-4 text-left">{t('choosing.headers.shows')}</th>
              <th className="nx-eyebrow py-2 text-left">{t('choosing.headers.action')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.name} className="border-b border-[var(--nx-hairline-soft)] last:border-0">
                <td className="py-3 pr-4 align-top font-[590] whitespace-nowrap">{row.name}</td>
                <td className="text-muted-foreground py-3 pr-4 align-top leading-5">{row.when}</td>
                <td className="text-muted-foreground py-3 pr-4 align-top leading-5">{row.shows}</td>
                <td className="text-muted-foreground py-3 align-top leading-5">{row.action}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
