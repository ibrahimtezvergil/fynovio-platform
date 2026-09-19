import { stageMeta } from '@/components/common/StageBadge'
import { i18n } from '@/lib/i18n'
import { STAGES } from '@/types'
import type { BoardCard, BoardColumn } from '@/features/demo-kanban/types'

/**
 * The sales board is the pipeline seen sideways — same stages, same order,
 * same tones as the grid and the badges. `STAGES` stays the single source of
 * display order; a board that reordered them would be a second truth.
 */
const STAGE_META = stageMeta()

export const CRM_COLUMNS: BoardColumn[] = STAGES.map((stage) => ({
  id: stage,
  label: STAGE_META[stage].label,
  tone: STAGE_META[stage].tone,
}))

function card(id: string): { title: string; context: string; tags: string[] } {
  return {
    title: i18n.t(`crm.cards.${id}.title`, { ns: 'demo-kanban' }),
    context: i18n.t(`crm.cards.${id}.context`, { ns: 'demo-kanban' }),
    tags: i18n.t(`crm.cards.${id}.tags`, { ns: 'demo-kanban', returnObjects: true }) as string[],
  }
}

export const CRM_CARDS: BoardCard[] = [
  { id: 'c-1', columnId: 'new', ...card('c-1'), owner: 'Jonas Weber', metric: '₺412.000', due: '7 Eki', priority: 'normal', comments: 2 },
  { id: 'c-2', columnId: 'new', ...card('c-2'), owner: 'Deniz Kaya', metric: '₺132.000', due: '15 Ara', priority: 'low' },
  { id: 'c-3', columnId: 'new', ...card('c-3'), owner: 'Mira Sandström', metric: '₺96.000', due: '20 Ara', priority: 'low' },

  { id: 'c-4', columnId: 'contacted', ...card('c-4'), owner: 'Mira Sandström', metric: '₺246.000', due: '22 Eki', priority: 'normal', comments: 4 },
  { id: 'c-5', columnId: 'contacted', ...card('c-5'), owner: 'Mira Sandström', metric: '₺148.250', due: '2 Ara', priority: 'normal' },

  { id: 'c-6', columnId: 'quoted', ...card('c-6'), owner: 'Selin Arslan', metric: '₺640.500', due: '30 Eyl', priority: 'high', comments: 7 },
  { id: 'c-7', columnId: 'quoted', ...card('c-7'), owner: 'Jonas Weber', metric: '₺172.900', due: '19 Kas', priority: 'normal' },

  { id: 'c-8', columnId: 'meeting', ...card('c-8'), owner: 'Deniz Kaya', metric: '₺862.000', due: '18 Eyl', priority: 'urgent', comments: 11 },
  { id: 'c-9', columnId: 'meeting', ...card('c-9'), owner: 'Deniz Kaya', metric: '₺318.750', due: '12 Eyl', overdue: true, priority: 'high', comments: 3 },

  { id: 'c-10', columnId: 'ready', ...card('c-10'), owner: 'Selin Arslan', metric: '₺184.400', due: '4 Kas', priority: 'high', comments: 1 },

  { id: 'c-11', columnId: 'onhold', ...card('c-11'), owner: 'Selin Arslan', metric: '₺96.500', priority: 'low' },
]
