import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** "Mira Sandström" -> "MS". First and last word only; single words give one letter. */
export function initialsOf(name: string): string {
  const words = name.trim().split(/\s+/)
  if (words.length === 0) return ''
  const first = words[0]?.[0] ?? ''
  const last = words.length > 1 ? (words[words.length - 1]?.[0] ?? '') : ''
  return (first + last).toLocaleUpperCase('tr-TR')
}
