import { i18n } from '@/lib/i18n'
import type { SavedView } from '@/features/demo-filters/types'

/**
 * Saved views are filters with a name. They exist because the three or four
 * combinations a team actually uses get rebuilt from scratch every morning
 * otherwise — and rebuilt slightly differently each time.
 *
 * Built once at import time, so — like `stageMeta()` — it renders whatever
 * language was active on load rather than switching live.
 */
export const SAVED_VIEWS: SavedView[] = [
  {
    id: 'all',
    label: i18n.t('savedViews.all.label', { ns: 'demo-filters' }),
    description: i18n.t('savedViews.all.description', { ns: 'demo-filters' }),
    filter: {},
    sort: [{ field: 'createdAt', direction: 'desc' }],
  },
  {
    id: 'waiting',
    label: i18n.t('savedViews.waiting.label', { ns: 'demo-filters' }),
    description: i18n.t('savedViews.waiting.description', { ns: 'demo-filters' }),
    filter: { statuses: ['pending'] },
    sort: [{ field: 'createdAt', direction: 'asc' }],
  },
  {
    id: 'high-value',
    label: i18n.t('savedViews.highValue.label', { ns: 'demo-filters' }),
    description: i18n.t('savedViews.highValue.description', { ns: 'demo-filters' }),
    filter: { statuses: ['pending', 'approved', 'shipped'], amount: { min: 100_000, max: null } },
    sort: [{ field: 'amount', direction: 'desc' }],
  },
  {
    id: 'risk',
    label: i18n.t('savedViews.risk.label', { ns: 'demo-filters' }),
    description: i18n.t('savedViews.risk.description', { ns: 'demo-filters' }),
    filter: { statuses: ['cancelled', 'refunded'] },
    sort: [{ field: 'amount', direction: 'desc' }],
  },
  {
    id: 'mine',
    label: i18n.t('savedViews.mine.label', { ns: 'demo-filters' }),
    description: i18n.t('savedViews.mine.description', { ns: 'demo-filters' }),
    filter: { owners: ['Deniz Kaya'] },
    sort: [
      { field: 'status', direction: 'asc' },
      { field: 'amount', direction: 'desc' },
    ],
  },
]
