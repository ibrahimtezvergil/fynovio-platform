import { GitCompareArrows } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'

const CHOICE_IDS = ['drawer', 'dialog', 'fullPage', 'inline'] as const

/** Four surfaces for "show me more about this row", and the question each answers. */
export function ChoosingSection() {
  const { t } = useTranslation('demo-drawers')

  return (
    <DemoSection
      id="secim"
      title={t('choosing.title')}
      description={t('choosing.description')}
      icon={GitCompareArrows}
      columns={1}
    >
      <div className="overflow-x-auto rounded-lg border border-[var(--nx-hairline)]">
        <table className="nx-grid">
          <thead>
            <tr>
              <th scope="col">{t('choosing.headers.surface')}</th>
              <th scope="col">{t('choosing.headers.when')}</th>
              <th scope="col">{t('choosing.headers.why')}</th>
              <th scope="col">{t('choosing.headers.avoid')}</th>
            </tr>
          </thead>
          <tbody>
            {CHOICE_IDS.map((id) => (
              <tr key={id}>
                <td className="name align-top">{t(`choosing.choices.${id}.surface`)}</td>
                <td className="align-top whitespace-normal">{t(`choosing.choices.${id}.when`)}</td>
                <td className="align-top whitespace-normal">{t(`choosing.choices.${id}.why`)}</td>
                <td className="align-top whitespace-normal">{t(`choosing.choices.${id}.avoid`)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </DemoSection>
  )
}
