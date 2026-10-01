import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { customFieldKeys } from '@/lib/custom-fields/api'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import { manageSharedViewResultSchema, sharedViewsSchema, type SharedView, type ViewColumn } from './schema'

export const sharedViewKeys = {
  list: (tenantId: number | null) => ['shared-views', tenantId] as const,
}

/** The tenant's shared table views (active and deprecated). Readable by every CRM reader, so any list page can offer them. */
export function useSharedViews() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useQuery({
    queryKey: sharedViewKeys.list(tenantId),
    enabled: tenantId !== null,
    retry: false,
    throwOnError: false,
    staleTime: 60_000,
    queryFn: async (): Promise<SharedView[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.crmSettings.views)
      return parseApiResponse(data, sharedViewsSchema)
    },
  })
}

export type ManageSharedViewVariables =
  | { operation: 'create'; key: string; name: string; sortOrder: number; columns: ViewColumn[]; idempotencyKey: string }
  | { operation: 'update'; id: number; expectedRowVersion: number; name: string; sortOrder: number; columns: ViewColumn[]; idempotencyKey: string }
  | { operation: 'deprecate' | 'reactivate'; id: number; expectedRowVersion: number; idempotencyKey: string }

export function useManageSharedView() {
  const { t } = useTranslation('opportunities')
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({
    name: 'crmSettings.sharedView',
    mutationFn: async (variables: ManageSharedViewVariables) => {
      const headers = { 'Idempotency-Key': variables.idempotencyKey }
      const { data } = variables.operation === 'create'
        ? await apiClient.post<unknown>(endpoints.crmSettings.views, { key: variables.key, name: variables.name, sortOrder: variables.sortOrder, columns: variables.columns }, { headers })
        : variables.operation === 'update'
          ? await apiClient.put<unknown>(endpoints.crmSettings.view(variables.id), {
            name: variables.name, sortOrder: variables.sortOrder, columns: variables.columns, expectedRowVersion: variables.expectedRowVersion,
          }, { headers })
          : await apiClient.post<unknown>(endpoints.crmSettings.viewTransition(variables.id, variables.operation), { expectedRowVersion: variables.expectedRowVersion }, { headers })
      return parseApiResponse(data, manageSharedViewResultSchema)
    },
    successToast: (_data, variables) => ({ title: t(`views.toast.${variables.operation}`) }),
    errorToast: () => null,
    // A field's impact lists the views that use it, so the field queries go stale with the views.
    invalidateKeys: () => [sharedViewKeys.list(tenantId), customFieldKeys.definitions(tenantId)],
  })
}
