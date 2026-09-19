import { paths } from '@/routes/paths'

export type SearchResultType = 'customer' | 'quote' | 'order'

export interface SearchResultItem {
  id: string
  type: SearchResultType
  label: string
  subtitle: string
  url: string
}

/**
 * V1's whole "entity search" — a small, in-memory mock index. No backend, no
 * fuzzy-search dependency: `cmdk`'s own filter (see `CommandPalette`) already
 * does the matching, this just supplies the rows. Same organizations and
 * document numbers as `src/mocks/handlers/home.ts`, so a search result, an
 * attention item and a recent-work row about "ACME Holding" always agree.
 */
export const MOCK_SEARCH_ENTITIES: SearchResultItem[] = [
  { id: 'acme', type: 'customer', label: 'ACME Holding', subtitle: 'Müşteri', url: paths.crmPipeline },
  { id: 'q-2381', type: 'quote', label: 'Q-2381', subtitle: 'ACME Holding', url: paths.crmPipeline },
  { id: 'so-23891', type: 'order', label: 'SO-23891', subtitle: 'ACME Holding', url: paths.crmPipeline },
  { id: 'baltic-freight', type: 'customer', label: 'Baltic Freight AB', subtitle: 'Müşteri', url: paths.crmPipeline },
  { id: 'q-8392', type: 'quote', label: 'Q-8392', subtitle: 'Baltic Freight AB', url: paths.crmPipeline },
  { id: 'so-291', type: 'order', label: 'SO-291', subtitle: 'Baltic Freight AB', url: paths.crmPipeline },
  { id: 'meridian', type: 'customer', label: 'Meridian Retail Group', subtitle: 'Müşteri', url: paths.crmPipeline },
  { id: 'q-1292', type: 'quote', label: 'Q-1292', subtitle: 'Meridian Retail Group', url: paths.crmPipeline },
  { id: 'ege-yapi', type: 'customer', label: 'Ege Yapı Malzeme', subtitle: 'Müşteri', url: paths.crmPipeline },
]
