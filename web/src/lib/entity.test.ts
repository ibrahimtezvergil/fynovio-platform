import { describe, expect, it } from 'vitest'
import { buildEntityRef } from '@/lib/entity'

describe('buildEntityRef', () => {
  it('preserves the stable identity and optional display metadata', () => {
    expect(buildEntityRef('deal', 'd-1', 'Renewal', '/pipeline', 'Nordwind')).toEqual({
      type: 'deal',
      id: 'd-1',
      display: 'Renewal',
      subtitle: 'Nordwind',
      url: '/pipeline',
    })
  })
})
