import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { setAuthToken } from '@/api/client'
import type { User } from '@/types'

/**
 * Session identity only. The login request itself is a React Query mutation
 * (`useLogin`) — this store never talks to the network, it just remembers who
 * came back from it.
 */
interface AuthState {
  isAuthenticated: boolean
  user: User | null
  token: string | null
  setSession: (user: User, token: string) => void
  logout: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      isAuthenticated: false,
      user: null,
      token: null,

      setSession: (user, token) => {
        setAuthToken(token)
        set({ isAuthenticated: true, user, token })
      },

      logout: () => {
        setAuthToken(null)
        set({ isAuthenticated: false, user: null, token: null })
      },
    }),
    {
      name: 'fynovio-auth',
      partialize: (s) => ({ isAuthenticated: s.isAuthenticated, user: s.user, token: s.token }),
      onRehydrateStorage: () => (state) => {
        if (state?.token) setAuthToken(state.token)
      },
    },
  ),
)
