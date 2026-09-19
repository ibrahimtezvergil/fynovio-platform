import DOMPurify from 'dompurify'

const ALLOWED_TAGS = [
  'p',
  'h2',
  'h3',
  'ul',
  'ol',
  'li',
  'blockquote',
  'a',
  'code',
  'strong',
  'em',
  'u',
  's',
  'br',
]

/** Sanitizes HTML emitted by RichTextInput before it is rendered again. */
export function sanitizeHtml(html: string): string {
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS,
    ALLOWED_ATTR: ['href'],
  })
}
