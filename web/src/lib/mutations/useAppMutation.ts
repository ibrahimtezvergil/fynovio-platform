import { useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { ZodType } from 'zod'
import { track } from '@/lib/telemetry'
import type { ApiError } from '@/types'

export interface AppMutationToast {
  title: string
  description?: string
}

export interface AppMutationUndo<TData, TVariables> {
  label: string
  onClick: (data: TData, variables: TVariables) => void | Promise<void>
}

export interface AppMutationConfig<TData, TVariables> {
  /** Becomes the `mutationKey` (so the global `mutation_error` tracker in `queryClient.ts` can name it) and the success telemetry event's discriminator. */
  name: string
  mutationFn: (variables: TVariables) => Promise<TData>
  /** Parsed before `mutationFn` runs; a failure short-circuits execute/toast-success and rejects in the same `ApiError.fields` shape a 422 would. */
  schema?: ZodType<TVariables>
  /** Cache writes — `setQueryData` for a value the mutation already computed, `queryClient` methods for anything else. Runs before the success toast. */
  onSuccess?: (data: TData, variables: TVariables) => void
  /** Query keys to invalidate after `onSuccess` — omit for the direct-cache-write pattern (see `QUERY_ARCHITECTURE.md`). */
  invalidateKeys?: (data: TData, variables: TVariables) => QueryKey[]
  /** Omit to stay silent — not every mutation should toast (see the calendar move). */
  successToast?: (data: TData, variables: TVariables) => AppMutationToast | null
  /** Defaults to the normalized `ApiError.message`. Return `null` when the caller renders the failure inline instead. */
  errorToast?: (error: ApiError, variables: TVariables) => AppMutationToast | null
  undo?: AppMutationUndo<TData, TVariables>
}

/**
 * The mutation wrapper Claude Task B (#12/#13/#22) exists to introduce: every
 * feature's `useMutation` call goes through this from now on instead of
 * repeating validate/toast/telemetry by hand. One request in flight —
 * validate (optional zod, in the 422 field-path shape) → execute →
 * cache write / invalidate → toast → telemetry → optional undo.
 */
export function useAppMutation<TData, TVariables>(config: AppMutationConfig<TData, TVariables>) {
  const queryClient = useQueryClient()

  return useMutation<TData, ApiError, TVariables>({
    mutationKey: [config.name],
    mutationFn: async (variables) => {
      if (config.schema) {
        const parsed = config.schema.safeParse(variables)
        if (!parsed.success) {
          const error: ApiError = {
            message: 'Geçersiz veri.',
            status: 0,
            fields: parsed.error.flatten().fieldErrors as Record<string, string[]>,
          }
          throw error
        }
        variables = parsed.data
      }
      return config.mutationFn(variables)
    },
    onSuccess: async (data, variables) => {
      config.onSuccess?.(data, variables)

      const keys = config.invalidateKeys?.(data, variables)
      if (keys) {
        await Promise.all(keys.map((queryKey) => queryClient.invalidateQueries({ queryKey })))
      }

      const success = config.successToast?.(data, variables)
      if (success) {
        const undo = config.undo
        toast.success(success.title, {
          description: success.description,
          action: undo ? { label: undo.label, onClick: () => undo.onClick(data, variables) } : undefined,
        })
      }

      track(`mutation_${config.name}_success`)
    },
    onError: (error, variables) => {
      const failure = config.errorToast ? config.errorToast(error, variables) : { title: error.message }
      if (failure) toast.error(failure.title, { description: failure.description })
      // `mutation_error` telemetry is already emitted globally by the
      // MutationCache in `src/api/queryClient.ts`, keyed on `mutationKey`.
    },
  })
}
