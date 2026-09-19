import type { ComponentType, ReactElement } from 'react'

export interface LazyFeatureRoute {
  path: string
  protected: boolean
  load: () => Promise<{ default: ComponentType }>
}

export interface StaticFeatureRoute {
  path: string
  protected: boolean
  element: ReactElement
}

/** A feature-owned route definition; the router turns lazy loaders into routes. */
export type FeatureRoute = LazyFeatureRoute | StaticFeatureRoute
