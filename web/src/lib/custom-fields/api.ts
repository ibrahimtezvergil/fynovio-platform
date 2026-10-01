import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import { customFieldDefinitionsSchema, customFieldImpactSchema, manageCustomFieldResultSchema, type CustomFieldDefinition, type CustomFieldType } from './schema'

export const customFieldKeys = {
  definitions: (tenantId: number | null) => ['custom-field-definitions', tenantId] as const,
  impact: (tenantId: number | null, id: number) => ['custom-field-definitions', tenantId, 'impact', id] as const,
}

/** Opportunity field definitions (Active and Deprecated). Readable by every CRM reader, so forms can render. */
export function useCustomFieldDefinitions() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useQuery({
    queryKey: customFieldKeys.definitions(tenantId),
    enabled: tenantId !== null,
    retry: false,
    throwOnError: false,
    staleTime: 60_000,
    queryFn: async (): Promise<CustomFieldDefinition[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.crmSettings.customFields)
      return parseApiResponse(data, customFieldDefinitionsSchema)
    },
  })
}

/** How many opportunities hold a value for the field — shown before deprecating it. */
export function useCustomFieldImpact(id: number | null) {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useQuery({
    queryKey: customFieldKeys.impact(tenantId, id ?? 0),
    enabled: tenantId !== null && id !== null,
    retry: false,
    throwOnError: false,
    staleTime: 0,
    queryFn: async () => {
      const { data } = await apiClient.get<unknown>(endpoints.crmSettings.customFieldImpact(id!))
      return parseApiResponse(data, customFieldImpactSchema)
    },
  })
}

export interface CustomFieldConfigInput {
  options?: { key: string; label: string; isDeprecated: boolean }[] | null
  scale?: number | null
  min?: number | null
  max?: number | null
  maxLength?: number | null
  target?: { boundedContext: string; entityType: string } | null
}

export type ManageCustomFieldVariables =
  | { operation: 'create'; key: string; label: string; type: CustomFieldType; isRequired: boolean; sortOrder: number; config: CustomFieldConfigInput | null; idempotencyKey: string }
  | { operation: 'update'; id: number; expectedRowVersion: number; label: string; isRequired: boolean; sortOrder: number; config: CustomFieldConfigInput | null; idempotencyKey: string }
  | { operation: 'deprecate' | 'reactivate'; id: number; expectedRowVersion: number; idempotencyKey: string }

export function useManageCustomField() {
  const { t } = useTranslation('opportunities')
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({
    name: 'crmSettings.customField',
    mutationFn: async (variables: ManageCustomFieldVariables) => {
      const headers = { 'Idempotency-Key': variables.idempotencyKey }
      const { data } = variables.operation === 'create'
        ? await apiClient.post<unknown>(endpoints.crmSettings.customFields, {
          key: variables.key, label: variables.label, type: variables.type, isRequired: variables.isRequired, sortOrder: variables.sortOrder, config: variables.config,
        }, { headers })
        : variables.operation === 'update'
          ? await apiClient.put<unknown>(endpoints.crmSettings.customField(variables.id), {
            label: variables.label, isRequired: variables.isRequired, sortOrder: variables.sortOrder, config: variables.config, expectedRowVersion: variables.expectedRowVersion,
          }, { headers })
          : await apiClient.post<unknown>(endpoints.crmSettings.customFieldTransition(variables.id, variables.operation), { expectedRowVersion: variables.expectedRowVersion }, { headers })
      return parseApiResponse(data, manageCustomFieldResultSchema)
    },
    successToast: (_data, variables) => ({ title: t(`customFields.toast.${variables.operation}`) }),
    errorToast: () => null,
    invalidateKeys: () => [customFieldKeys.definitions(tenantId)],
  })
}
