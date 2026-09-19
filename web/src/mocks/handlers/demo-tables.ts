import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import {
  DEPARTMENTS,
  type Employee,
} from '@/features/demo-tables/data/employees'
import { API_BASE } from '@/mocks/apiBase'

const FIRST_NAMES = [
  'Deniz', 'Selin', 'Jonas', 'Mira', 'Emre', 'Lotta', 'Kerem', 'Ayla', 'Tobias', 'Nadia',
  'Baran', 'Elif', 'Anders', 'Zeynep', 'Marek', 'Ipek', 'Onur', 'Freya', 'Cem', 'Sanne',
]

const LAST_NAMES = [
  'Kaya', 'Arslan', 'Weber', 'Sandström', 'Yilmaz', 'Bergman', 'Demir', 'Novak', 'Lindqvist',
  'Aydın', 'Hoffmann', 'Ferrari', 'Öztürk', 'Janssen', 'Doğan', 'Kowalski',
]

const ROLES = [
  'Account Executive', 'Backend Engineer', 'Customer Success', 'Data Analyst',
  'Engineering Manager', 'Finance Lead', 'Frontend Engineer', 'Operations Specialist',
  'Product Designer', 'Sales Manager', 'Support Engineer', 'Workspace Admin',
]

const LOCATIONS = [
  'İstanbul', 'Ankara', 'İzmir', 'Stockholm', 'Berlin', 'Amsterdam', 'Gdańsk', 'Remote',
]

/**
 * Deterministic PRNG (mulberry32). The dataset must be byte-identical on every
 * run — a fresh random array would invalidate every row model and, in the
 * worst case, loop the adapter.
 */
function mulberry32(seed: number): () => number {
  let state = seed >>> 0
  return () => {
    state = (state + 0x6d2b79f5) >>> 0
    let t = Math.imul(state ^ (state >>> 15), 1 | state)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function pick<T>(random: () => number, values: readonly T[]): T {
  return values[Math.floor(random() * values.length)]
}

function generateEmployees(count: number): Employee[] {
  const random = mulberry32(20260904)
  const employees: Employee[] = []
  const seen = new Set<string>()

  for (let index = 0; index < count; index += 1) {
    const first = pick(random, FIRST_NAMES)
    const last = pick(random, LAST_NAMES)
    const name = `${first} ${last}`

    // Local part stays unique even when a name repeats.
    let local = `${first}.${last}`
      .toLocaleLowerCase('en-US')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z.]/g, '')
    if (seen.has(local)) local = `${local}${index}`
    seen.add(local)

    const daysAgo = Math.floor(random() * 180)
    const lastActive = new Date(Date.UTC(2026, 8, 4) - daysAgo * 86_400_000)

    employees.push({
      id: `emp_${String(index + 1).padStart(4, '0')}`,
      name,
      email: `${local}@fynovio.com`,
      department: pick(random, DEPARTMENTS),
      role: pick(random, ROLES),
      status: random() > 0.82 ? (random() > 0.5 ? 'invited' : 'suspended') : 'active',
      location: pick(random, LOCATIONS),
      seats: 1 + Math.floor(random() * 12),
      monthlySpend: Math.round((180 + random() * 4_800) / 10) * 10,
      lastActive: lastActive.toISOString().slice(0, 10),
    })
  }

  return employees
}

/** Module-scope constant: one array, one identity, for the life of the tab. */
const EMPLOYEES: Employee[] = generateEmployees(184)

/**
 * Kept in sync with `canGlobalFilterColumn` in `coreAdminTable.tsx`: the server
 * must search exactly the columns the client would, or toggling the mode
 * switch would quietly change the result set.
 */
const SEARCHABLE: Array<keyof Employee> = ['name', 'department', 'role']

function matches(employee: Employee, search: string): boolean {
  if (!search) return true
  const needle = search.toLocaleLowerCase('en-US')
  return SEARCHABLE.some((key) => String(employee[key]).toLocaleLowerCase('en-US').includes(needle))
}

function compare(a: Employee, b: Employee, id: string): number {
  const left = a[id as keyof Employee]
  const right = b[id as keyof Employee]
  if (typeof left === 'number' && typeof right === 'number') return left - right
  return String(left).localeCompare(String(right), 'en')
}

interface SortToken {
  id: string
  desc: boolean
}

function parseSort(raw: string | null): SortToken[] {
  if (!raw) return []
  return raw
    .split(',')
    .map((token) => token.trim())
    .filter(Boolean)
    .map((token) => (token.startsWith('-') ? { id: token.slice(1), desc: true } : { id: token, desc: false }))
}

export const demoTableHandlers = [
  /** Stands in for `GET /employees?page=&size=&sort=&q=` — server mode. */
  http.get(`${API_BASE}${endpoints.demoTables.employees}`, async ({ request }) => {
    await delay(320)
    const url = new URL(request.url)
    const pageIndex = Number(url.searchParams.get('pageIndex') ?? '0')
    const pageSize = Number(url.searchParams.get('pageSize') ?? '10')
    const search = url.searchParams.get('search') ?? ''
    const sorting = parseSort(url.searchParams.get('sort'))

    const filtered = EMPLOYEES.filter((employee) => matches(employee, search))
    const sorted = sorting.length
      ? [...filtered].sort((a, b) => {
          for (const entry of sorting) {
            const result = compare(a, b, entry.id)
            if (result !== 0) return entry.desc ? -result : result
          }
          return 0
        })
      : filtered

    const start = pageIndex * pageSize
    return HttpResponse.json({
      rows: sorted.slice(start, start + pageSize),
      rowCount: filtered.length,
    })
  }),

  /** Client mode: the whole table, processed in the browser by the row models. */
  http.get(`${API_BASE}${endpoints.demoTables.employeesAll}`, async () => {
    await delay(320)
    return HttpResponse.json(EMPLOYEES)
  }),
]
