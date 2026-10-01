import { z } from 'zod'

/** A column of a shared table view: a built-in column of the table, or a tenant field by its immutable key. */
export const viewColumnSchema = z.object({ kind: z.enum(['builtin', 'field']), key: z.string() })
export type ViewColumn = z.infer<typeof viewColumnSchema>

/** Built-in columns a view may place (the server's allow-list; the table's two placeholder columns are not on it). */
export const BUILT_IN_VIEW_COLUMNS = ['id', 'createdAt', 'updatedAt', 'party', 'owner', 'needs', 'status', 'stage', 'amount', 'totalAmount', 'expiryDate'] as const
export type BuiltInViewColumn = (typeof BUILT_IN_VIEW_COLUMNS)[number]

export const MAX_VIEW_COLUMNS = 20

export const sharedViewSchema = z.object({
  id: z.number().int().positive(),
  key: z.string(),
  name: z.string(),
  kind: z.string(),
  columns: z.array(viewColumnSchema),
  status: z.enum(['Active', 'Deprecated']),
  sortOrder: z.number().int(),
  rowVersion: z.number().int().positive(),
})
export type SharedView = z.infer<typeof sharedViewSchema>

export const sharedViewsSchema = z.array(sharedViewSchema)
export const manageSharedViewResultSchema = z.object({ definitionId: z.number(), rowVersion: z.number(), replayed: z.boolean(), changeSetId: z.number().nullish() })
