import { i18n } from '@/lib/i18n'
import type { BoardCard, BoardColumn } from '@/features/demo-kanban/types'

function hours(count: number): string {
  return i18n.t('card.hours', { ns: 'demo-kanban', count })
}

function card(id: string): { title: string; context: string; tags: string[] } {
  return {
    title: i18n.t(`erp.cards.${id}.title`, { ns: 'demo-kanban' }),
    context: i18n.t(`erp.cards.${id}.context`, { ns: 'demo-kanban' }),
    tags: i18n.t(`erp.cards.${id}.tags`, { ns: 'demo-kanban', returnObjects: true }) as string[],
  }
}

/**
 * The task board. Unlike the sales board its columns are a *process*, not a
 * funnel, so two of them carry WIP limits: the point of the board is to show
 * where work piled up, and a column with no cap can never say so.
 */
export const ERP_COLUMNS: BoardColumn[] = [
  { id: 'backlog', label: i18n.t('erp.columns.backlog', { ns: 'demo-kanban' }), tone: 'gray' },
  { id: 'planned', label: i18n.t('erp.columns.planned', { ns: 'demo-kanban' }), tone: 'blue' },
  { id: 'progress', label: i18n.t('erp.columns.progress', { ns: 'demo-kanban' }), tone: 'amber', wipLimit: 3 },
  { id: 'review', label: i18n.t('erp.columns.review', { ns: 'demo-kanban' }), tone: 'purple', wipLimit: 2 },
  { id: 'done', label: i18n.t('erp.columns.done', { ns: 'demo-kanban' }), tone: 'green' },
]

export const ERP_CARDS: BoardCard[] = [
  { id: 't-1', columnId: 'backlog', ...card('t-1'), owner: 'Ece Yıldırım', metric: hours(3), priority: 'low' },
  { id: 't-2', columnId: 'backlog', ...card('t-2'), owner: 'Onur Şahin', metric: hours(5), priority: 'normal' },
  { id: 't-3', columnId: 'backlog', ...card('t-3'), owner: 'Ece Yıldırım', metric: hours(13), priority: 'normal' },

  { id: 't-4', columnId: 'planned', ...card('t-4'), owner: 'Burak Demir', metric: hours(8), due: '11 Eyl', priority: 'high', checklist: { done: 0, total: 4 } },
  { id: 't-5', columnId: 'planned', ...card('t-5'), owner: 'Ece Yıldırım', metric: hours(13), due: '16 Eyl', priority: 'normal', checklist: { done: 1, total: 6 } },
  { id: 't-12', columnId: 'planned', ...card('t-12'), owner: 'Burak Demir', metric: hours(13), due: '13 Eyl', priority: 'normal', checklist: { done: 2, total: 6 } },

  { id: 't-6', columnId: 'progress', ...card('t-6'), owner: 'Burak Demir', metric: hours(21), due: '9 Eyl', priority: 'urgent', checklist: { done: 5, total: 9 }, comments: 6 },
  { id: 't-7', columnId: 'progress', ...card('t-7'), owner: 'Onur Şahin', metric: hours(13), due: '4 Eyl', overdue: true, priority: 'high', checklist: { done: 3, total: 5 }, comments: 2 },
  { id: 't-8', columnId: 'progress', ...card('t-8'), owner: 'Ece Yıldırım', metric: hours(8), due: '12 Eyl', priority: 'normal', checklist: { done: 2, total: 3 } },
  { id: 't-9', columnId: 'progress', ...card('t-9'), owner: 'Burak Demir', metric: hours(5), due: '10 Eyl', priority: 'high', checklist: { done: 1, total: 2 } },

  { id: 't-10', columnId: 'review', ...card('t-10'), owner: 'Onur Şahin', metric: hours(21), due: '8 Eyl', priority: 'high', checklist: { done: 7, total: 7 }, comments: 4 },
  { id: 't-11', columnId: 'review', ...card('t-11'), owner: 'Ece Yıldırım', metric: hours(8), due: '9 Eyl', priority: 'normal', checklist: { done: 4, total: 4 } },

  { id: 't-13', columnId: 'done', ...card('t-13'), owner: 'Onur Şahin', metric: hours(13), priority: 'normal', checklist: { done: 8, total: 8 } },
  { id: 't-14', columnId: 'done', ...card('t-14'), owner: 'Burak Demir', metric: hours(3), priority: 'low', checklist: { done: 3, total: 3 } },
]
