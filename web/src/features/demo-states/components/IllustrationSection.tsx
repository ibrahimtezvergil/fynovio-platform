import { Shapes } from 'lucide-react'
import type { ComponentType, SVGProps } from 'react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import {
  AllDoneIllustration,
  BrokenIllustration,
  EmptyBoxIllustration,
  NoAccessIllustration,
  NoResultsIllustration,
  OfflineIllustration,
} from '@/components/common/illustrations'

interface Entry {
  name: string
  meaning: string
  Art: ComponentType<SVGProps<SVGSVGElement>>
  tone: string
}

/**
 * The artwork on its own, without the copy around it. Every drawing is a
 * 160×120 SVG whose strokes are `currentColor` and whose fills are tokens —
 * one file covers both themes, and the parent decides the tone.
 */
export function IllustrationSection() {
  const { t } = useTranslation('demo-states')

  const entries: readonly Entry[] = [
    {
      name: 'EmptyBoxIllustration',
      meaning: t('illustrations.entries.emptyBox'),
      Art: EmptyBoxIllustration,
      tone: 'text-[var(--nx-tint)]',
    },
    {
      name: 'NoResultsIllustration',
      meaning: t('illustrations.entries.noResults'),
      Art: NoResultsIllustration,
      tone: 'text-[var(--nx-tint)]',
    },
    {
      name: 'BrokenIllustration',
      meaning: t('illustrations.entries.broken'),
      Art: BrokenIllustration,
      tone: 'text-[var(--nx-st-red-fg)]',
    },
    {
      name: 'OfflineIllustration',
      meaning: t('illustrations.entries.offline'),
      Art: OfflineIllustration,
      tone: 'text-[var(--nx-st-amber-fg)]',
    },
    {
      name: 'NoAccessIllustration',
      meaning: t('illustrations.entries.noAccess'),
      Art: NoAccessIllustration,
      tone: 'text-[var(--nx-tint)]',
    },
    {
      name: 'AllDoneIllustration',
      meaning: t('illustrations.entries.allDone'),
      Art: AllDoneIllustration,
      tone: 'text-[var(--nx-st-green-fg)]',
    },
  ]

  return (
    <DemoSection
      id="illustrasyon"
      title={t('illustrations.title')}
      description={t('illustrations.description')}
      icon={Shapes}
      columns={1}
    >
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {entries.map(({ name, meaning, Art, tone }) => (
          <figure
            key={name}
            className="flex flex-col items-center gap-2.5 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-4 pt-5 pb-4 text-center"
          >
            <Art className={`w-full max-w-[150px] ${tone}`} />
            <figcaption className="flex flex-col gap-0.5">
              <span className="text-[12.5px] font-[590]">{meaning}</span>
              <code className="text-muted-foreground font-mono text-[11px]">{name}</code>
            </figcaption>
          </figure>
        ))}
      </div>
    </DemoSection>
  )
}
