import { describe, expect, it } from 'vitest'
import { paths } from '@/routes/paths'
import { returnUrlQuery, sanitizeReturnUrl } from './returnUrl'

describe('sanitizeReturnUrl', () => {
  it.each([
    ['protocol-relative', '//evil.com'],
    ['protocol-relative with path', '//evil.com/x'],
    ['backslash host', '/\\evil.com'],
    ['absolute https', 'https://evil.com'],
    ['absolute http', 'http://evil.com/x'],
    ['javascript scheme', 'javascript:alert(1)'],
    ['data scheme', 'data:text/html,<script>alert(1)</script>'],
    ['encoded protocol-relative', '%2F%2Fevil.com'],
    ['slash then encoded slash', '/%2Fevil.com'],
    ['slash then encoded backslash', '/%5Cevil.com'],
    ['tab inside', '/\t/evil.com'],
    // Dot segments collapse to `//host` once the URL is normalized — the check must hold on the output too.
    ['dot segment before protocol-relative', '/.//evil.com'],
    ['parent segment before protocol-relative', '/a/..//evil.com'],
    ['dot segment before backslash host', '/./\\evil.com'],
    ['encoded dot segment before protocol-relative', '/%2e//evil.com'],
    ['encoded parent segments before protocol-relative', '/%2E%2E//evil.com'],
    ['newline inside', '/foo\nbar'],
    ['NUL byte', '/foo\u0000'],
    ['no leading slash', 'crm/pipeline'],
    ['malformed percent escape', '/%E0%A4%A'],
    ['empty', ''],
  ])('falls back to the dashboard for %s', (_label, value) => {
    expect(sanitizeReturnUrl(value)).toBe(paths.dashboard)
  })

  it('falls back for null and undefined', () => {
    expect(sanitizeReturnUrl(null)).toBe(paths.dashboard)
    expect(sanitizeReturnUrl(undefined)).toBe(paths.dashboard)
  })

  it('rejects an overlong value', () => {
    expect(sanitizeReturnUrl(`/${'a'.repeat(3000)}`)).toBe(paths.dashboard)
  })

  it('keeps a valid same-origin path with query and hash', () => {
    expect(sanitizeReturnUrl('/crm/pipeline?x=1#y')).toBe('/crm/pipeline?x=1#y')
  })

  it('keeps an ordinary encoded path segment', () => {
    expect(sanitizeReturnUrl('/files/my%20report.pdf')).toBe('/files/my%20report.pdf')
  })

  it('uses the given fallback', () => {
    expect(sanitizeReturnUrl('//evil.com', '/crm')).toBe('/crm')
  })

  it.each([paths.login, paths.selectTenant, paths.noAccess])('never returns to the auth page %s (no bounce loop)', (page) => {
    expect(sanitizeReturnUrl(page)).toBe(paths.dashboard)
    expect(sanitizeReturnUrl(`${page}?returnUrl=%2Fcrm`)).toBe(paths.dashboard)
  })
})

describe('returnUrlQuery', () => {
  it('is empty for the dashboard and for anything unsafe', () => {
    expect(returnUrlQuery(paths.dashboard)).toBe('')
    expect(returnUrlQuery('//evil.com')).toBe('')
  })

  it('encodes a deep link', () => {
    expect(returnUrlQuery('/crm/pipeline?x=1')).toBe(`?returnUrl=${encodeURIComponent('/crm/pipeline?x=1')}`)
  })
})
