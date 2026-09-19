import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { useShallow } from 'zustand/react/shallow'

interface SmartDefaultsState {
  values: Record<string, unknown>
  remember: (scope: string, values: Record<string, unknown>) => void
}

/** Personal, device-local form memory. Server/role derived defaults remain a backend concern. */
export const useSmartDefaultsStore = create<SmartDefaultsState>()(
  persist(
    (set) => ({
      values: {},
      remember: (scope, values) => set((state) => ({ values: { ...state.values, ...Object.fromEntries(Object.entries(values).map(([key, value]) => [`${scope}.${key}`, value])) } })),
    }),
    { name: 'fynovio-smart-defaults' },
  ),
)

/** Reads a named form's last submitted values as a partial RHF default object. */
export function useSmartDefaults<T extends Record<string, unknown>>(scope: string): Partial<T> {
  return useSmartDefaultsStore(useShallow((state) => Object.fromEntries(
    Object.entries(state.values)
      .filter(([key]) => key.startsWith(`${scope}.`))
      .map(([key, value]) => [key.slice(scope.length + 1), value]),
  ))) as Partial<T>
}
