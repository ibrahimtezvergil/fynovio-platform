/** Domain row for both demo grids. Deliberately wider than any single grid shows. */
export interface Employee {
  id: string
  name: string
  email: string
  department: Department
  role: string
  status: EmployeeStatus
  location: string
  seats: number
  monthlySpend: number
  /** ISO date — sorted with `sortFn_datetime`, formatted at render time. */
  lastActive: string
}

export const DEPARTMENTS = [
  'Engineering',
  'Sales',
  'Operations',
  'Finance',
  'Support',
  'Marketing',
] as const
export type Department = (typeof DEPARTMENTS)[number]

export const EMPLOYEE_STATUSES = ['active', 'invited', 'suspended'] as const
export type EmployeeStatus = (typeof EMPLOYEE_STATUSES)[number]

/** Stable empty fallback — `?? []` inline would be a new array every render. */
export const NO_EMPLOYEES: Employee[] = []
