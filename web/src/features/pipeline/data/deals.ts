import type { Deal } from '@/types'

/** Stable identity for an empty result, so React Query never hands back a new []. */
export const NO_DEALS: Deal[] = []

export const OWNERS = ['Deniz Kaya', 'Selin Arslan', 'Jonas Weber', 'Mira Sandström'] as const
