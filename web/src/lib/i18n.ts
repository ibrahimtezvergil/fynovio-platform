import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'

import authEn from '@/locales/en/auth'
import calendarEn from '@/locales/en/calendar'
import companySettingsEn from '@/locales/en/company-settings'
import commonEn from '@/locales/en/common'
import conversationsEn from '@/locales/en/conversations'
import dashboardEn from '@/locales/en/dashboard'
import demoBadgesEn from '@/locales/en/demo-badges'
import demoChartsEn from '@/locales/en/demo-charts'
import demoDrawersEn from '@/locales/en/demo-drawers'
import demoFiltersEn from '@/locales/en/demo-filters'
import demoFormsEn from '@/locales/en/demo-forms'
import demoKanbanEn from '@/locales/en/demo-kanban'
import demoNotificationsEn from '@/locales/en/demo-notifications'
import demoOverlaysEn from '@/locales/en/demo-overlays'
import demoStatesEn from '@/locales/en/demo-states'
import demoTablesEn from '@/locales/en/demo-tables'
import demoTimelineEn from '@/locales/en/demo-timeline'
import homeEn from '@/locales/en/home'
import navEn from '@/locales/en/nav'
import notificationsEn from '@/locales/en/notifications'
import opportunitiesEn from '@/locales/en/opportunities'
import pipelineEn from '@/locales/en/pipeline'
import placeholderEn from '@/locales/en/placeholder'
import routesEn from '@/locales/en/routes'
import settingsEn from '@/locales/en/settings'

import authTr from '@/locales/tr/auth'
import calendarTr from '@/locales/tr/calendar'
import companySettingsTr from '@/locales/tr/company-settings'
import commonTr from '@/locales/tr/common'
import conversationsTr from '@/locales/tr/conversations'
import dashboardTr from '@/locales/tr/dashboard'
import demoBadgesTr from '@/locales/tr/demo-badges'
import demoChartsTr from '@/locales/tr/demo-charts'
import demoDrawersTr from '@/locales/tr/demo-drawers'
import demoFiltersTr from '@/locales/tr/demo-filters'
import demoFormsTr from '@/locales/tr/demo-forms'
import demoKanbanTr from '@/locales/tr/demo-kanban'
import demoNotificationsTr from '@/locales/tr/demo-notifications'
import demoOverlaysTr from '@/locales/tr/demo-overlays'
import demoStatesTr from '@/locales/tr/demo-states'
import demoTablesTr from '@/locales/tr/demo-tables'
import demoTimelineTr from '@/locales/tr/demo-timeline'
import homeTr from '@/locales/tr/home'
import navTr from '@/locales/tr/nav'
import notificationsTr from '@/locales/tr/notifications'
import opportunitiesTr from '@/locales/tr/opportunities'
import pipelineTr from '@/locales/tr/pipeline'
import placeholderTr from '@/locales/tr/placeholder'
import routesTr from '@/locales/tr/routes'
import settingsTr from '@/locales/tr/settings'

/**
 * One namespace per feature slice (mirrors `src/features/<name>/`), plus
 * `common` (shared `components/common/**`), `nav` (layouts) and `routes`
 * (route-level shells like NotFoundPage). Keeps a catalog file's ownership
 * as unambiguous as the feature-slice boundary it mirrors.
 */
export const i18n = i18next.createInstance()

void i18n.use(initReactI18next).init({
  fallbackLng: 'tr',
  supportedLngs: ['tr', 'en'],
  defaultNS: 'common',
  interpolation: { escapeValue: false },
  resources: {
    tr: {
      auth: authTr,
      calendar: calendarTr,
      'company-settings': companySettingsTr,
      common: commonTr,
      conversations: conversationsTr,
      dashboard: dashboardTr,
      'demo-badges': demoBadgesTr,
      'demo-charts': demoChartsTr,
      'demo-drawers': demoDrawersTr,
      'demo-filters': demoFiltersTr,
      'demo-forms': demoFormsTr,
      'demo-kanban': demoKanbanTr,
      'demo-notifications': demoNotificationsTr,
      'demo-overlays': demoOverlaysTr,
      'demo-states': demoStatesTr,
      'demo-tables': demoTablesTr,
      'demo-timeline': demoTimelineTr,
      home: homeTr,
      nav: navTr,
      notifications: notificationsTr,
      opportunities: opportunitiesTr,
      pipeline: pipelineTr,
      placeholder: placeholderTr,
      routes: routesTr,
      settings: settingsTr,
    },
    en: {
      auth: authEn,
      calendar: calendarEn,
      'company-settings': companySettingsEn,
      common: commonEn,
      conversations: conversationsEn,
      dashboard: dashboardEn,
      'demo-badges': demoBadgesEn,
      'demo-charts': demoChartsEn,
      'demo-drawers': demoDrawersEn,
      'demo-filters': demoFiltersEn,
      'demo-forms': demoFormsEn,
      'demo-kanban': demoKanbanEn,
      'demo-notifications': demoNotificationsEn,
      'demo-overlays': demoOverlaysEn,
      'demo-states': demoStatesEn,
      'demo-tables': demoTablesEn,
      'demo-timeline': demoTimelineEn,
      home: homeEn,
      nav: navEn,
      notifications: notificationsEn,
      opportunities: opportunitiesEn,
      pipeline: pipelineEn,
      placeholder: placeholderEn,
      routes: routesEn,
      settings: settingsEn,
    },
  },
})

export default i18n
