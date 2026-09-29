import { describe, expect, it } from 'vitest'
import { draftFromQuery } from './partyDraft'

describe('draftFromQuery', () => {
  it.each([
    ['Acme Ltd', { name: 'Acme Ltd', phone: '', email: '' }],
    ['  Ada Lovelace ', { name: 'Ada Lovelace', phone: '', email: '' }],
    ['info@acme.test', { name: '', phone: '', email: 'info@acme.test' }],
    ['+90 532 111 22 33', { name: '', phone: '+90 532 111 22 33', email: '' }],
    ['(0212) 999-88-77', { name: '', phone: '(0212) 999-88-77', email: '' }],
    ['1001', { name: '1001', phone: '', email: '' }],
    ['', { name: '', phone: '', email: '' }],
  ])('sorts %j into the right field', (query, expected) => {
    expect(draftFromQuery(query)).toEqual(expected)
  })
})
