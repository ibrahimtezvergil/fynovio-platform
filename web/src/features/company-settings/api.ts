import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import { type CompanySettings, companySettingsSchema, type CompanySettingsFormValues, updateCompanySettingsResultSchema } from './schema'

const IDEMPOTENCY_HEADER = 'Idempotency-Key'

/** Tenant-scoped settings cache: changing tenant must never show another company's identity. */
export const companySettingsKeys = {
  all: (tenantId: number | null) => ['company-settings', tenantId] as const,
  profile: (tenantId: number | null) => [...companySettingsKeys.all(tenantId), 'profile'] as const,
}

export function useCompanySettings() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useQuery({
    queryKey: companySettingsKeys.profile(tenantId),
    enabled: tenantId !== null,
    retry: false,
    throwOnError: false,
    queryFn: async (): Promise<CompanySettings> => {
      const { data } = await apiClient.get<unknown>(endpoints.companySettings.profile)
      return parseApiResponse(data, companySettingsSchema)
    },
  })
}

export interface UpdateCompanySettingsVariables {
  values: CompanySettingsFormValues
  expectedVersion: number
  idempotencyKey: string
}

/** The tenant is derived from the access token; this client never accepts or sends a tenant id. */
export function useUpdateCompanySettings() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({
    name: 'companySettings.update',
    mutationFn: async ({ values, expectedVersion, idempotencyKey }: UpdateCompanySettingsVariables) => {
      const { data } = await apiClient.put<unknown>(
        endpoints.companySettings.profile,
        { ...values, expectedVersion },
        { headers: { [IDEMPOTENCY_HEADER]: idempotencyKey } },
      )
      return parseApiResponse(data, updateCompanySettingsResultSchema)
    },
    invalidateKeys: () => [companySettingsKeys.profile(tenantId)],
  })
}
