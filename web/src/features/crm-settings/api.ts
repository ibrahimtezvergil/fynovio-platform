import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import { crmSettingsSchema, type CrmSettings } from './schema'

const key = (tenantId: number | null) => ['crm-settings', tenantId] as const
export function useCrmSettings() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useQuery({ queryKey: key(tenantId), enabled: tenantId !== null, retry: false, throwOnError: false,
    queryFn: async (): Promise<CrmSettings> => { const { data } = await apiClient.get<unknown>(endpoints.crmSettings.root); return parseApiResponse(data, crmSettingsSchema) },
  })
}
export function useUpdateCrmSettings() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({ name: 'crmSettings.update', mutationFn: async (values: Pick<CrmSettings, 'defaultPipelineDefinitionId' | 'opportunityCreationMode' | 'opportunityCreationSteps' | 'defaultOpportunityTypeId' | 'requireLostReason' | 'requireWonLine' | 'defaultAssignmentMode' | 'assignmentPolicy' | 'defaultPrincipal' | 'defaultTeamId' | 'defaultTerritoryId'> & { expectedVersion: number; idempotencyKey: string }) => {
    const { idempotencyKey, expectedVersion, defaultPrincipal, ...settings } = values
    await apiClient.put(endpoints.crmSettings.root, { ...settings, expectedVersion,
      defaultPrincipalIssuer: defaultPrincipal?.issuer || null, defaultPrincipalSubject: defaultPrincipal?.subject || null }, { headers: { 'Idempotency-Key': idempotencyKey } })
  }, invalidateKeys: () => [key(tenantId), ['crm-pipeline-stages', tenantId, 'default']] })
}

export function useCreatePipelineDraft() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({ name: 'crmSettings.pipelineDraft', mutationFn: async (payload: Record<string, unknown> & { idempotencyKey: string }) => {
    const { idempotencyKey, ...body } = payload
    const { data } = await apiClient.post(endpoints.crmSettings.pipelineDrafts, body, { headers: { 'Idempotency-Key': idempotencyKey } })
    return data as { pipelineDefinitionId: number; versionId: number; versionNumber: number; rowVersion: number; replayed: boolean }
  }, invalidateKeys: () => [key(tenantId)] })
}
export function usePublishPipelineVersion() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({ name: 'crmSettings.pipelinePublish', mutationFn: async ({ pipelineId, versionId, expectedPipelineRowVersion, idempotencyKey }: { pipelineId: number; versionId: number; expectedPipelineRowVersion: number; idempotencyKey: string }) => {
    const { data } = await apiClient.post(endpoints.crmSettings.pipelinePublish(pipelineId, versionId), { expectedPipelineRowVersion }, { headers: { 'Idempotency-Key': idempotencyKey } })
    return data
  }, invalidateKeys: () => [key(tenantId), ['crm-pipeline-stages', tenantId, 'default']] })
}
export function useSetPipelineLifecycle() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({ name: 'crmSettings.pipelineLifecycle', mutationFn: async ({ pipelineId, ...body }: { pipelineId: number; expectedRowVersion: number; isActive: boolean; archive: boolean; restore: boolean; idempotencyKey: string }) => {
    const { idempotencyKey, ...request } = body
    await apiClient.put(endpoints.crmSettings.pipelineLifecycle(pipelineId), request, { headers: { 'Idempotency-Key': idempotencyKey } })
  }, invalidateKeys: () => [key(tenantId)] })
}
export function useValidatePipelineDraft(pipelineId: number | null, versionId: number | null) {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useQuery({ queryKey: [...key(tenantId), 'pipeline-validation', pipelineId, versionId], enabled: tenantId !== null && pipelineId !== null && versionId !== null, retry: false, throwOnError: false,
    queryFn: async () => { const { data } = await apiClient.get(`/crm/settings/pipelines/${pipelineId}/versions/${versionId}/validate`); return data as { isValid: boolean; activeStageCount: number; entryStageCount: number; invalidTransitionCount: number; opportunitiesRetainedOnPriorVersions: number; errors: string[] } },
  })
}
export type CrmCatalogKind = 'opportunity-types' | 'lost-reasons' | 'customer-needs'
export interface CatalogMutation { id?: number; expectedVersion: number; name: string; key?: string; category?: string | null; averagePrice?: number; status: 'Active' | 'Inactive' | 'Archived'; idempotencyKey: string }
export function useManageCrmCatalog(kind: CrmCatalogKind) {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  return useAppMutation({ name: `crmSettings.catalog.${kind}`, mutationFn: async (payload: CatalogMutation) => {
    const { idempotencyKey, id, ...body } = payload
    const url = id == null ? endpoints.crmSettings.catalog(kind) : endpoints.crmSettings.catalogItem(kind, id)
    const { data } = id == null
      ? await apiClient.post(url, body, { headers: { 'Idempotency-Key': idempotencyKey } })
      : await apiClient.put(url, body, { headers: { 'Idempotency-Key': idempotencyKey } })
    return data
  }, invalidateKeys: () => [key(tenantId)] })
}
