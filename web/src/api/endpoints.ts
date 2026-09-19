/** Single place every request path is written down. */
export const endpoints = {
  auth: {
    login: '/auth/login',
    logout: '/auth/logout',
    me: '/auth/me',
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
    events: '/calendar/events',
  },
  demoTables: {
    employees: '/demo-tables/employees',
    employeesAll: '/demo-tables/employees/all',
  },
  demoForms: {
    customers: '/demo-forms/customers',
  },
} as const
