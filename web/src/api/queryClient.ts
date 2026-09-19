import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { track } from '@/lib/telemetry'

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error, query) => {
      track('query_error', { queryKey: query.queryKey, message: errorMessage(error) })
    },
  }),
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      track('mutation_error', {
        mutationKey: mutation.options.mutationKey,
        message: errorMessage(error),
      })
    },
  }),
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      retry: 1,
      refetchOnWindowFocus: false,
      // Query errors bubble to the nearest ErrorBoundary / errorElement
      // instead of silently sitting in `isError` with nothing reading it.
      throwOnError: true,
    },
  },
})
