export interface PartyDraft {
  name: string
  phone: string
  email: string
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const PHONE = /^\+?[\d\s().-]{6,}$/

/** What the person typed in the customer search, sorted into the field it most likely belongs to, so "create" needs no retyping. */
export function draftFromQuery(query: string): PartyDraft {
  const text = query.trim()
  if (EMAIL.test(text)) return { name: '', phone: '', email: text }
  if (PHONE.test(text) && /\d{6,}/.test(text.replace(/\D/g, ''))) return { name: '', phone: text, email: '' }
  return { name: text, phone: '', email: '' }
}
