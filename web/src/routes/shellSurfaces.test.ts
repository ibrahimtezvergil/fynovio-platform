import type { ReactElement } from 'react'
import { matchRoutes, Navigate } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { buildNavGroups, buildSidebarNav, findNavTrail } from '@/layouts/navigation'
import { i18n } from '@/lib/i18n'
import type { NavScope } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
import { router } from '@/routes'

const t = i18n.getFixedT('tr', 'nav')

function surfaceOf(pathname: string) {
  const handle = matchRoutes(router.routes, pathname)
    ?.map((match) => match.route.handle as { surface?: string; navScope?: NavScope } | undefined)
    .find((value) => value?.surface)
  return handle
}

describe('Settings shell surface', () => {
  it('lives at /profile/settings on a surface with no sidebar', () => {
    expect(paths.settings).toBe('/profile/settings')
    expect(surfaceOf(paths.settings)).toEqual({ surface: 'utility' })
  })

  it('is on no sidebar, but still resolves for the breadcrumb and the palette', () => {
    const scopes: NavScope[] = ['crm', 'developer']
    for (const scope of scopes) {
      const ids = buildSidebarNav(t, scope).flatMap((group) => group.items.map((item) => item.id))
      expect(ids).not.toContain('settings')
    }
    expect(findNavTrail(buildNavGroups(t), paths.settings)?.item.id).toBe('settings')
  })

  it('keeps the old /settings URL alive as a redirect to /profile/settings', () => {
    const leaf = matchRoutes(router.routes, paths.legacySettings)?.at(-1)?.route.element as ReactElement<{ to: string; replace?: boolean }>
    expect(leaf.type).toBe(Navigate)
    expect(leaf.props).toMatchObject({ to: paths.settings, replace: true })
  })
})

describe('Members shell surface', () => {
  it('has no sidebar of its own', () => {
    expect(surfaceOf(paths.members)).toEqual({ surface: 'utility' })
  })
})
