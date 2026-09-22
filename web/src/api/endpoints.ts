/** Single place every request path is written down. */
export const endpoints = {
  auth: {
    login: '/auth/login',
    refresh: '/auth/refresh',
    logout: '/auth/logout',
    selectTenant: '/auth/tenants/select',
    me: '/auth/me',
    config: '/auth/config',
    validateInvitation: '/auth/invitations/validate',
    acceptInvitation: '/auth/invitations/accept',
    forgotPassword: '/auth/password/forgot',
    resetPassword: '/auth/password/reset',
    changePassword: '/auth/password/change',
    register: '/auth/register',
  },
  opportunities: {
    list: '/opportunities',
    create: '/opportunities',
    detail: (id: number) => `/opportunities/${id}`,
    actions: (id: number) => `/opportunities/${id}/actions`,
    addLine: (id: number) => `/opportunities/${id}/lines`,
    cancelLine: (id: number, lineId: number) => `/opportunities/${id}/lines/${lineId}/cancel`,
    open: (id: number) => `/opportunities/${id}/open`,
    changeStage: (id: number) => `/opportunities/${id}/stage`,
    win: (id: number) => `/opportunities/${id}/win`,
    lose: (id: number) => `/opportunities/${id}/lose`,
    reassign: (id: number) => `/opportunities/${id}/reassign`,
    assignablePrincipals: (id: number) => `/opportunities/${id}/assignable-principals`,
  },
  references: {
    parties: '/crm/references/parties',
  },
  pipelines: {
    stages: (versionId: number) => `/pipelines/${versionId}/stages`,
  },
  deals: {
    list: '/deals',
    detail: (id: string) => `/deals/${id}`,
  },
  dashboard: {
    deals: '/dashboard/deals',
    stages: '/dashboard/stages',
    activities: '/dashboard/activities',
  },
  home: {
    attention: '/home/attention',
    recentWork: '/home/recent-work',
    teamActivity: '/home/team-activity',
  },
  pipeline: {
    deals: '/pipeline/deals',
  },
  calendar: {
    entries: '/calendar/entries',
    entry: (id: number) => `/calendar/entries/${id}`,
  },
  companySettings: {
    profile: '/company/settings',
    access: '/company/settings/access',
    invitations: '/company/settings/invitations',
    roleAssignments: '/company/settings/role-assignments',
    roleAssignment: (id: number) => `/company/settings/role-assignments/${id}`,
  },
  demoTables: {
    employees: '/demo-tables/employees',
    employeesAll: '/demo-tables/employees/all',
  },
  demoForms: {
    customers: '/demo-forms/customers',
  },
} as const
