import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { SortingState } from '@tanstack/react-table'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { isArrayOf, unwrapApiResponse } from '@/api/response'
import type { Employee } from '@/features/demo-tables/data/employees'

export interface EmployeePageRequest {
  pageIndex: number
  pageSize: number
  sorting: SortingState
  search: string
}

export interface EmployeePage {
  rows: Employee[]
  /** Total matching rows on the server, not the length of `rows`. */
  rowCount: number
}

function isEmployeePage(value: unknown): value is EmployeePage {
  return (
    typeof value === 'object' &&
    value !== null &&
    'rows' in value &&
    'rowCount' in value &&
    Array.isArray(value.rows) &&
    typeof value.rowCount === 'number'
  )
}

export const demoTableKeys = {
  all: ['demo-tables'] as const,
  employees: () => [...demoTableKeys.all, 'employees'] as const,
  list: () => [...demoTableKeys.employees(), 'list'] as const,
  page: (request: EmployeePageRequest) => [...demoTableKeys.employees(), 'page', request] as const,
}

function serializeSorting(sorting: SortingState): string {
  return sorting.map((entry) => (entry.desc ? `-${entry.id}` : entry.id)).join(',')
}

/**
 * `GET /demo-tables/employees?pageIndex=&pageSize=&sort=&search=`, served by
 * the MSW handler at `src/mocks/handlers/demo-tables.ts`.
 *
 * The table is told about the server doing this work through
 * `manualFiltering` / `manualSorting` / `manualPagination`, which only mean
 * "skip the client row models" — they never issue a request. Driving that
 * request from the same state is this hook's job.
 */
async function fetchEmployeePage(request: EmployeePageRequest): Promise<EmployeePage> {
  const { data } = await apiClient.get<unknown>(endpoints.demoTables.employees, {
    params: {
      pageIndex: request.pageIndex,
      pageSize: request.pageSize,
      sort: serializeSorting(request.sorting),
      search: request.search,
    },
  })
  return unwrapApiResponse(data, isEmployeePage)
}

/** Server mode: one page at a time, total supplied through `rowCount`. */
export function useEmployeePage(request: EmployeePageRequest, enabled: boolean) {
  return useQuery({
    queryKey: demoTableKeys.page(request),
    queryFn: () => fetchEmployeePage(request),
    enabled,
    // Keeps the previous page on screen while the next one loads, so the grid
    // never collapses to an empty body between pages.
    placeholderData: keepPreviousData,
  })
}

/** Client mode: the whole table, processed in the browser by the row models. */
export function useAllEmployees(enabled: boolean) {
  return useQuery({
    queryKey: demoTableKeys.list(),
    queryFn: async (): Promise<Employee[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.demoTables.employeesAll)
      return unwrapApiResponse(data, isArrayOf<Employee>)
    },
    enabled,
    staleTime: 5 * 60 * 1000,
  })
}
