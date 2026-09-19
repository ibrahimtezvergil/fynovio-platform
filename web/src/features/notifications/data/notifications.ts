import { i18n } from '@/lib/i18n'
import { minutesAgo } from '@/lib/datetime'
import type { AppNotification } from '@/features/notifications/types'

const tn = (key: string) => i18n.t(key, { ns: 'notifications' })

/**
 * The seed the bell opens with. Swapping in the real endpoint replaces this
 * file plus the store's initial state; nothing else in the feature moves.
 *
 * Ordered newest first — the center never sorts, it renders what it is given.
 *
 * Title/body/decision copy is resolved from the `notifications` catalog at
 * module-load time (via the shared `i18n` instance directly, since this file
 * has no component to hook into) — it renders whatever language was active
 * when the app started, not a live switch. See `stageMeta()` in
 * `StageBadge.tsx` for the same accepted tradeoff on other seed/fixture data.
 */
export const SEED_NOTIFICATIONS: AppNotification[] = [
  {
    id: 'n-1',
    kind: 'approval',
    title: tn('seed.n1.title'),
    body: tn('seed.n1.body'),
    at: minutesAgo(3),
    read: false,
    actor: 'Deniz Kaya',
    decision: { accept: tn('seed.decision.approve'), reject: tn('seed.decision.reject') },
  },
  {
    id: 'n-2',
    kind: 'mention',
    title: tn('seed.n2.title'),
    body: tn('seed.n2.body'),
    at: minutesAgo(26),
    read: false,
    actor: 'Selin Arslan',
  },
  {
    id: 'n-3',
    kind: 'system',
    title: tn('seed.n3.title'),
    body: tn('seed.n3.body'),
    at: minutesAgo(52),
    read: false,
  },
  {
    id: 'n-4',
    kind: 'task',
    title: tn('seed.n4.title'),
    body: tn('seed.n4.body'),
    at: minutesAgo(95),
    read: false,
    actor: 'Jonas Weber',
  },
  {
    id: 'n-5',
    kind: 'reminder',
    title: tn('seed.n5.title'),
    body: tn('seed.n5.body'),
    at: minutesAgo(170),
    read: true,
  },
  {
    id: 'n-6',
    kind: 'task',
    title: tn('seed.n6.title'),
    body: tn('seed.n6.body'),
    at: minutesAgo(320),
    read: true,
  },
  {
    id: 'n-7',
    kind: 'system',
    title: tn('seed.n7.title'),
    body: tn('seed.n7.body'),
    at: minutesAgo(1_490),
    read: true,
  },
  {
    id: 'n-8',
    kind: 'approval',
    title: tn('seed.n8.title'),
    body: tn('seed.n8.body'),
    at: minutesAgo(1_760),
    read: true,
    actor: 'Mira Sandström',
  },
]
