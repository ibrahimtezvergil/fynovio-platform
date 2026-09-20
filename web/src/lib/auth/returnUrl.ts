import { paths } from '@/routes/paths'

const MAX_LENGTH = 2048
// eslint-disable-next-line no-control-regex -- rejecting control characters is the point
const CONTROL_CHARS = /[\u0000-\u001f\u007f]/
const PLACEHOLDER_ORIGIN = 'http://return-url.invalid'
/** Never send someone "back" to a page that would immediately bounce them again. */
const AUTH_PAGES: readonly string[] = [paths.login, paths.selectTenant, paths.noAccess]

function isSafeRelative(value: string): boolean {
  if (CONTROL_CHARS.test(value)) return false
  if (!value.startsWith('/')) return false
  // Protocol-relative (`//host`) and its backslash variants — browsers read `\` as `/`.
  if (value.startsWith('//') || value.startsWith('/\\')) return false
  return true
}

/**
 * The one place a post-login destination is trusted. Accepts only a same-origin,
 * relative path (+ query + hash); anything else — absolute URLs, `//host`,
 * `/\host`, `javascript:`, encoded variants, control characters — falls back to
 * `fallback`. The check runs on the raw value AND on its decoded form, and the
 * result is parsed against a placeholder origin so a value that changes origin
 * when parsed is rejected.
 */
export function sanitizeReturnUrl(raw: string | null | undefined, fallback: string = paths.dashboard): string {
  if (!raw || raw.length > MAX_LENGTH) return fallback
  if (!isSafeRelative(raw)) return fallback

  let decoded: string
  try {
    decoded = decodeURIComponent(raw)
  } catch {
    return fallback
  }
  if (!isSafeRelative(decoded)) return fallback

  let url: URL
  try {
    url = new URL(raw, PLACEHOLDER_ORIGIN)
  } catch {
    return fallback
  }
  if (url.origin !== PLACEHOLDER_ORIGIN) return fallback
  if (AUTH_PAGES.includes(url.pathname)) return fallback

  // Parsing collapses dot segments (`/.//host` → `//host`), so the shape check must hold on the output too.
  const normalized = `${url.pathname}${url.search}${url.hash}`
  return isSafeRelative(normalized) ? normalized : fallback
}

/** `?returnUrl=` query string for `target`, omitted when it would just be the dashboard. */
export function returnUrlQuery(target: string): string {
  const safe = sanitizeReturnUrl(target)
  return safe === paths.dashboard ? '' : `?returnUrl=${encodeURIComponent(safe)}`
}
