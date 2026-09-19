import { paths } from '@/routes/paths'
import type { ApplicationItem } from './types'

/**
 * Central Application Registry — no JSX call site hardcodes an app tile.
 * Read by the Home launcher, the Topbar Applications/Tools menus, and (via
 * the same nav contributions) Cmd+K. Settings is deliberately absent: it's a
 * System-owned page (its own rail, its own breadcrumb path), not a global
 * application or tool.
 */
export const applicationRegistry: ApplicationItem[] = [
  { id: 'crm', kind: 'application', status: 'available', labelKey: 'applications.crm.label', descriptionKey: 'applications.crm.description', icon: 'crm', route: paths.crm, navScope: 'crm', badge: 38, order: 1 },
  { id: 'sales', kind: 'application', status: 'comingSoon', labelKey: 'applications.sales.label', descriptionKey: 'applications.sales.description', icon: 'sales', order: 2 },
  { id: 'marketing', kind: 'application', status: 'comingSoon', labelKey: 'applications.marketing.label', descriptionKey: 'applications.marketing.description', icon: 'marketing', order: 3 },
  { id: 'service', kind: 'application', status: 'comingSoon', labelKey: 'applications.service.label', descriptionKey: 'applications.service.description', icon: 'service', order: 4 },
  { id: 'purchasing', kind: 'application', status: 'comingSoon', labelKey: 'applications.purchasing.label', descriptionKey: 'applications.purchasing.description', icon: 'purchasing', order: 5 },
  { id: 'inventory-warehouse', kind: 'application', status: 'comingSoon', labelKey: 'applications.inventoryWarehouse.label', descriptionKey: 'applications.inventoryWarehouse.description', icon: 'inventoryWarehouse', order: 6 },
  { id: 'supply-chain-planning', kind: 'application', status: 'comingSoon', labelKey: 'applications.supplyChainPlanning.label', descriptionKey: 'applications.supplyChainPlanning.description', icon: 'supplyChainPlanning', order: 7 },
  { id: 'production', kind: 'application', status: 'comingSoon', labelKey: 'applications.production.label', descriptionKey: 'applications.production.description', icon: 'production', order: 8 },
  { id: 'quality', kind: 'application', status: 'comingSoon', labelKey: 'applications.quality.label', descriptionKey: 'applications.quality.description', icon: 'quality', order: 9 },
  { id: 'asset-maintenance', kind: 'application', status: 'comingSoon', labelKey: 'applications.assetMaintenance.label', descriptionKey: 'applications.assetMaintenance.description', icon: 'assetMaintenance', order: 10 },
  { id: 'product-plm', kind: 'application', status: 'comingSoon', labelKey: 'applications.productPlm.label', descriptionKey: 'applications.productPlm.description', icon: 'productPlm', order: 11 },
  { id: 'finance-accounting', kind: 'application', status: 'comingSoon', labelKey: 'applications.finance.label', descriptionKey: 'applications.finance.description', icon: 'finance', order: 12 },
  { id: 'projects', kind: 'application', status: 'comingSoon', labelKey: 'applications.projects.label', descriptionKey: 'applications.projects.description', icon: 'projects', order: 13 },
  { id: 'human-resources', kind: 'application', status: 'comingSoon', labelKey: 'applications.humanResources.label', descriptionKey: 'applications.humanResources.description', icon: 'humanResources', order: 14 },
  { id: 'b2b-commerce', kind: 'application', status: 'comingSoon', labelKey: 'applications.b2bCommerce.label', descriptionKey: 'applications.b2bCommerce.description', icon: 'b2bCommerce', order: 15 },
  { id: 'document-management', kind: 'application', status: 'comingSoon', labelKey: 'applications.documentManagement.label', descriptionKey: 'applications.documentManagement.description', icon: 'documentManagement', order: 16 },
  { id: 'inbox', kind: 'utility', status: 'comingSoon', labelKey: 'applications.inbox.label', descriptionKey: 'applications.inbox.description', icon: 'inbox', order: 1 },
  { id: 'calendar', kind: 'utility', status: 'comingSoon', labelKey: 'applications.calendar.label', descriptionKey: 'applications.calendar.description', icon: 'calendar', route: paths.calendar, order: 2 },
  { id: 'tasks', kind: 'utility', status: 'comingSoon', labelKey: 'applications.tasks.label', descriptionKey: 'applications.tasks.description', icon: 'tasks', order: 3 },
  { id: 'conversations', kind: 'utility', status: 'comingSoon', labelKey: 'applications.conversations.label', descriptionKey: 'applications.conversations.description', icon: 'conversations', order: 4 },
  { id: 'notes', kind: 'utility', status: 'comingSoon', labelKey: 'applications.notes.label', descriptionKey: 'applications.notes.description', icon: 'notes', order: 5 },
  { id: 'files', kind: 'utility', status: 'comingSoon', labelKey: 'applications.files.label', descriptionKey: 'applications.files.description', icon: 'files', route: paths.files, order: 6 },
  { id: 'knowledge-base', kind: 'utility', status: 'comingSoon', labelKey: 'applications.knowledgeBase.label', descriptionKey: 'applications.knowledgeBase.description', icon: 'knowledgeBase', order: 7 },
  { id: 'approvals', kind: 'utility', status: 'comingSoon', labelKey: 'applications.approvals.label', descriptionKey: 'applications.approvals.description', icon: 'approvals', order: 8 },
  { id: 'reports-analytics', kind: 'utility', status: 'comingSoon', labelKey: 'applications.reportsAnalytics.label', descriptionKey: 'applications.reportsAnalytics.description', icon: 'reportsAnalytics', order: 9 },
  { id: 'automations', kind: 'utility', status: 'comingSoon', labelKey: 'applications.automations.label', descriptionKey: 'applications.automations.description', icon: 'automations', order: 10 },
  { id: 'ai-copilot', kind: 'utility', status: 'comingSoon', labelKey: 'applications.aiCopilot.label', descriptionKey: 'applications.aiCopilot.description', icon: 'aiCopilot', order: 11 },
]

export type { ApplicationItem, ApplicationKind, ApplicationStatus, ApplicationIconKey } from './types'
