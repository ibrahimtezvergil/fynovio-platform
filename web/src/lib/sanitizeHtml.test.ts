import { describe, expect, it } from 'vitest'
import { sanitizeHtml } from '@/lib/sanitizeHtml'

describe('sanitizeHtml', () => {
  it('keeps the markup RichTextInput is allowed to emit', () => {
    expect(sanitizeHtml('<h2>Scope</h2><p><strong>12 months</strong> <a href="https://fynovio.com">details</a></p>'))
      .toBe('<h2>Scope</h2><p><strong>12 months</strong> <a href="https://fynovio.com">details</a></p>')
  })

  it('removes executable tags and event handlers', () => {
    expect(sanitizeHtml('<p>Safe</p><script>alert(1)</script><img src=x onerror=alert(1)>'))
      .toBe('<p>Safe</p>')
  })

  it('rejects javascript links', () => {
    expect(sanitizeHtml('<a href="javascript:alert(1)">Unsafe</a>')).toBe('<a>Unsafe</a>')
  })
})
