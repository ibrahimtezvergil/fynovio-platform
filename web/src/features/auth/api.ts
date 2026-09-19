import { useMutation } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import type { LoginValues } from '@/features/auth/schema'
import { useAuthStore } from '@/features/auth/store/useAuthStore'
import type { User } from '@/types'
import { userSchema } from '@/types/schemas'
import { z } from 'zod'

const sessionSchema: z.ZodType<{ user: User; token: string }> = z.object({
  user: userSchema,
  token: z.string(),
})

/**
 * The login round-trip, served by the MSW handler at
 * `src/mocks/handlers/auth.ts` until `/auth/login` is real.
 *
 * Request state (pending, error) belongs to the mutation, not to the store: the
 * store holds who is signed in, which is the only part that outlives the form.
 */
export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession)

  return useMutation({
    mutationFn: async (values: LoginValues): Promise<{ user: User; token: string }> => {
      const { data } = await apiClient.post<unknown>(
        endpoints.auth.login,
        values,
      )
      return parseApiResponse(data, sessionSchema)
    },
    onSuccess: ({ user, token }) => setSession(user, token),
  })
}
