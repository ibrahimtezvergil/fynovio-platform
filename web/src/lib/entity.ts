import type { EntityRef } from '@/types/entity'

export function buildEntityRef(
  type: string,
  id: string,
  display: string,
  url: string,
  subtitle?: string,
): EntityRef {
  return subtitle === undefined
    ? { type, id, display, url }
    : { type, id, display, subtitle, url }
}
