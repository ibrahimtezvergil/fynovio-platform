import type { SelectOption } from '@/components/common/inputs'
import { i18n } from '@/lib/i18n'

/**
 * Every string below resolves from the `demo-overlays` catalog at module-load
 * time via the shared `i18n` instance — this is fixture data, not a
 * component, so there is no hook to re-run on a language change. See
 * `stageMeta()` in `StageBadge.tsx` for the same accepted tradeoff elsewhere.
 */
const t = (key: string, options?: Record<string, unknown>) =>
  i18n.t(key, { ns: 'demo-overlays', ...options })

/** Long body copy for the scrollable-modal demo — length is the point. */
export const CONSENT_PARAGRAPHS: readonly string[] = i18n.t('content.consentParagraphs', {
  ns: 'demo-overlays',
  returnObjects: true,
}) as string[]

export const DEAL_STAGES: readonly SelectOption[] = [
  { value: 'yeni', label: t('content.dealStages.yeni.label'), description: t('content.dealStages.yeni.description') },
  { value: 'nitelendirme', label: t('content.dealStages.nitelendirme.label'), description: t('content.dealStages.nitelendirme.description') },
  { value: 'teklif', label: t('content.dealStages.teklif.label'), description: t('content.dealStages.teklif.description') },
  { value: 'pazarlik', label: t('content.dealStages.pazarlik.label'), description: t('content.dealStages.pazarlik.description') },
  { value: 'kazanildi', label: t('content.dealStages.kazanildi.label'), description: t('content.dealStages.kazanildi.description') },
]

export const DEAL_OWNERS: readonly SelectOption[] = [
  { value: 'zeynep', label: 'Zeynep Arslan', description: t('content.dealOwners.zeynep') },
  { value: 'mert', label: 'Mert Doğan', description: t('content.dealOwners.mert') },
  { value: 'elif', label: 'Elif Yıldırım', description: t('content.dealOwners.elif') },
  { value: 'can', label: 'Can Öztürk', description: t('content.dealOwners.can') },
]

/** Rows for the sheet demo's detail panel — a deal's activity trail. */
export const ACTIVITY_TRAIL: readonly { time: string; title: string; note: string }[] = [
  {
    time: t('content.activityTrail.viewed.time'),
    title: t('content.activityTrail.viewed.title'),
    note: t('content.activityTrail.viewed.note', { email: 'satinalma@acme.com.tr' }),
  },
  {
    time: t('content.activityTrail.revisionSent.time'),
    title: t('content.activityTrail.revisionSent.title'),
    note: t('content.activityTrail.revisionSent.note'),
  },
  {
    time: t('content.activityTrail.meetingNote.time'),
    title: t('content.activityTrail.meetingNote.title'),
    note: t('content.activityTrail.meetingNote.note'),
  },
  {
    time: t('content.activityTrail.stageChanged.time'),
    title: t('content.activityTrail.stageChanged.title'),
    note: t('content.activityTrail.stageChanged.note'),
  },
  {
    time: t('content.activityTrail.dealCreated.time'),
    title: t('content.activityTrail.dealCreated.title'),
    note: t('content.activityTrail.dealCreated.note'),
  },
]
