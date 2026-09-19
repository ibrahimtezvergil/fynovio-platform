import { useTranslation } from 'react-i18next'
import { StatusBadge, type StatusMeta, type StatusRegistry } from '@/components/common/StatusBadge'
import { i18n } from '@/lib/i18n'
import type { Stage } from '@/types'

const STAGE_TONE: Record<Stage, StatusMeta['tone']> = {
  new: 'blue',
  contacted: 'teal',
  quoted: 'amber',
  meeting: 'purple',
  ready: 'green',
  onhold: 'gray',
}

/**
 * Resolved from the `pipeline` catalog against whichever language is active
 * when this is called. Module-scope data files (kanban columns, the badges
 * gallery) call this once at import, so they render whatever language was
 * active on load rather than switching live — components should read the
 * reactive `useStageMeta()` below instead.
 */
export function stageMeta(): StatusRegistry<Stage> {
  return (Object.keys(STAGE_TONE) as Stage[]).reduce((meta, stage) => {
    meta[stage] = { label: i18n.t(`stage.${stage}`, { ns: 'pipeline' }), tone: STAGE_TONE[stage] }
    return meta
  }, {} as StatusRegistry<Stage>)
}

/** The reactive form — re-renders when the language changes. */
export function useStageMeta(): StatusRegistry<Stage> {
  const { t } = useTranslation('pipeline')
  return (Object.keys(STAGE_TONE) as Stage[]).reduce((meta, stage) => {
    meta[stage] = { label: t(`stage.${stage}`), tone: STAGE_TONE[stage] }
    return meta
  }, {} as StatusRegistry<Stage>)
}

/**
 * Stage never rides on colour alone — the dot plus the label carry it,
 * so it survives greyscale and colour-blind viewing (WCAG 1.4.1).
 *
 * In light mode the pill text darkens against its pastel fill; every
 * fill/text pair clears 6:1.
 */
export function StageBadge({ stage, className }: { stage: Stage; className?: string }) {
  const meta = useStageMeta()
  return <StatusBadge {...meta[stage]} className={className} />
}
