import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import { useSingleFlight } from '@/lib/mutations/useSingleFlight'
import type { ApiError } from '@/types'
import { toProblem, type Problem } from './problem'

interface CommandMutation<V extends { idempotencyKey: string }, R> {
  mutateAsync: (variables: V) => Promise<R>
  error: ApiError | null
  isPending: boolean
  reset: () => void
}

/**
 * Runs one logical user action against a command mutation: exactly one request in flight (a same-tick
 * double click is dropped, not queued), one idempotency key per logical action, held across an unknown
 * outcome and released on a definitive one (`AttemptKeys`). Resolves the result, or `undefined` when it failed;
 * the failure is exposed as a `Problem` for the caller to render inline.
 */
export function useKeyedCommand<V extends { idempotencyKey: string }, R>(mutation: CommandMutation<V, R>) {
  const keys = useAttemptKeys()
  const singleFlight = useSingleFlight()

  const run = async (payload: Omit<V, 'idempotencyKey'>): Promise<R | undefined> => {
    let result: R | undefined
    await singleFlight(async () => {
      const idempotencyKey = keys.begin(payload)
      try {
        result = await mutation.mutateAsync({ ...payload, idempotencyKey } as V)
        keys.settle(null)
      } catch (error) {
        keys.settle(error as ApiError)
      }
    })
    return result
  }

  const problem: Problem | null = mutation.error ? toProblem(mutation.error) : null
  return { run, problem, isPending: mutation.isPending, reset: mutation.reset }
}
