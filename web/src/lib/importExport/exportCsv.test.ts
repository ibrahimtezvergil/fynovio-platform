import { expect, it } from 'vitest'
import { rowsToCsv } from './exportCsv'
it('escapes CSV values', () => expect(rowsToCsv([{ name: 'Ada, "Ltd"' }], [{ key: 'name', header: 'Name' }])).toBe('"Name"\n"Ada, ""Ltd"""'))
