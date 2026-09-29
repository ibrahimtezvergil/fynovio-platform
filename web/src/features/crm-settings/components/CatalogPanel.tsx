import { ArchiveRestore, Archive, MoreHorizontal, Pencil, Power, PowerOff, Search } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Alert, AlertTitle } from '@/components/ui/alert'
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import type { ApiError } from '@/types'
import { useManageCrmCatalog, type CrmCatalogKind } from '../api'
import { SectionHeading } from './SectionHeading'

export type CatalogItem = { id: number; name: string; key?: string; category?: string | null; averagePrice?: number; status: 'Active' | 'Inactive' | 'Archived'; rowVersion: number }
type Status = CatalogItem['status']

/** A list this long earns a search box; shorter ones are read at a glance. */
const SEARCH_FROM = 6
const statusOrder: Record<Status, number> = { Active: 0, Inactive: 1, Archived: 2 }

const catalogKeyFromName = (name: string) => name.trim().normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase()
  .replace(/ı/g, 'i').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'lost-reason'

const price = (value: number) => new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)

interface CatalogPanelProps {
  kind: CrmCatalogKind
  title: string
  description: string
  items: readonly CatalogItem[]
}

/**
 * One list for every catalog (opportunity types, lost reasons, customer needs): the row shows what the entry is,
 * the primary action is Edit, and the state changes live in one menu so a row never carries four buttons.
 */
export function CatalogPanel({ kind, title, description, items }: CatalogPanelProps) {
  const { t } = useTranslation('opportunities')
  const mutation = useManageCrmCatalog(kind)
  const keys = useAttemptKeys()
  const [editing, setEditing] = useState<number | null>(null)
  const [archiveTarget, setArchiveTarget] = useState<CatalogItem | null>(null)
  const [search, setSearch] = useState('')
  const [name, setName] = useState('')
  const [keyValue, setKeyValue] = useState('')
  const [category, setCategory] = useState('')
  const [averagePrice, setAveragePrice] = useState('0')
  const [error, setError] = useState('')

  const visible = useMemo(() => {
    const needle = search.trim().toLocaleLowerCase()
    return items
      .filter((item) => !needle || [item.name, item.category, item.key].some((value) => value?.toLocaleLowerCase().includes(needle)))
      .toSorted((a, b) => statusOrder[a.status] - statusOrder[b.status])
  }, [items, search])

  const begin = (item?: CatalogItem) => {
    setEditing(item?.id ?? 0); setName(item?.name ?? ''); setKeyValue(item?.key ?? '')
    setCategory(item?.category ?? ''); setAveragePrice(String(item?.averagePrice ?? 0)); setError('')
  }
  const failure = (problem: unknown) => { const apiError = problem as ApiError; keys.settle(apiError); setError(apiError.status === 409 ? t('settings.conflict') : t('settings.saveError')) }
  const send = async (body: { id?: number; expectedVersion: number; name: string; key?: string; category?: string | null; averagePrice?: number; status: Status }) => {
    try { await mutation.mutateAsync({ ...body, idempotencyKey: keys.begin(body) }); keys.settle(null); setError(''); return true }
    catch (problem) { failure(problem); return false }
  }
  const save = async (event: FormEvent) => {
    event.preventDefault()
    const item = items.find((candidate) => candidate.id === editing)
    const saved = await send({ id: item?.id, expectedVersion: item?.rowVersion ?? 0, name: name.trim(), key: item?.key ?? (kind === 'lost-reasons' ? catalogKeyFromName(name) : keyValue.trim()),
      category: category.trim() || null, averagePrice: Number(averagePrice), status: item?.status ?? 'Active' })
    if (saved) setEditing(null)
  }
  const setStatus = (item: CatalogItem, status: Status) => send({ id: item.id, expectedVersion: item.rowVersion, name: item.name, key: item.key, category: item.category, averagePrice: item.averagePrice, status })

  return <Card className="min-w-0 gap-4 px-6 pt-[22px] pb-6">
    <div className="flex flex-wrap items-start justify-between gap-3"><SectionHeading title={title} description={description} /><Button type="button" variant="outline" onClick={() => begin()}>{t('settings.add')}</Button></div>
    {error && <Alert variant="destructive"><AlertTitle>{error}</AlertTitle></Alert>}

    {items.length >= SEARCH_FROM && <div className="relative max-w-sm">
      <Search aria-hidden className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" strokeWidth={1.7} />
      <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t('settings.catalog.search')} aria-label={t('settings.catalog.search')} className="pl-9" />
    </div>}

    {items.length === 0 && editing === null && <p className="text-muted-foreground rounded-[var(--nx-r-ctl)] border border-dashed px-4 py-8 text-center text-sm">{t('settings.catalogEmpty')}</p>}
    {items.length > 0 && visible.length === 0 && <p className="text-muted-foreground px-1 py-4 text-sm">{t('settings.catalog.noMatch')}</p>}

    {visible.length > 0 && <ul className="divide-y rounded-[var(--nx-r-card)] border">{visible.map((item) => {
      const archived = item.status === 'Archived'
      const meta = [kind === 'opportunity-types' ? item.key : null, item.category, kind === 'customer-needs' && item.averagePrice ? price(item.averagePrice) : null].filter(Boolean).join(' · ')
      return <li key={item.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5">
        <div className={`min-w-0 flex-1 ${archived ? 'text-muted-foreground' : ''}`}>
          <p className="truncate font-medium">{item.name}</p>
          {meta && <p className="text-muted-foreground truncate text-xs">{meta}</p>}
        </div>
        <Badge variant={item.status === 'Active' ? 'success' : archived ? 'secondary' : 'warning'}>{t(`settings.status.${item.status}`)}</Badge>
        {!archived && <Button type="button" variant="outline" size="sm" disabled={mutation.isPending} onClick={() => begin(item)}><Pencil aria-hidden strokeWidth={1.7} />{t('settings.edit')}</Button>}
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button type="button" variant="ghost" size="icon-sm" disabled={mutation.isPending} aria-label={t('settings.catalog.more', { name: item.name })}><MoreHorizontal aria-hidden /></Button>} />
          <DropdownMenuContent align="end" className="w-64">
            {archived
              ? <DropdownMenuItem onClick={() => void setStatus(item, 'Inactive')}><ArchiveRestore aria-hidden strokeWidth={1.7} />{t('settings.catalog.restore')}</DropdownMenuItem>
              : <>
                <DropdownMenuItem onClick={() => void setStatus(item, item.status === 'Active' ? 'Inactive' : 'Active')}>
                  {item.status === 'Active' ? <PowerOff aria-hidden strokeWidth={1.7} /> : <Power aria-hidden strokeWidth={1.7} />}
                  {item.status === 'Active' ? t('settings.deactivate') : t('settings.activate')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={() => setArchiveTarget(item)}><Archive aria-hidden strokeWidth={1.7} />{t('settings.archive')}</DropdownMenuItem>
              </>}
          </DropdownMenuContent>
        </DropdownMenu>
      </li>
    })}</ul>}

    {items.length > 0 && <p className="text-muted-foreground text-xs">{t('settings.catalog.legend')}</p>}

    {editing !== null && <form onSubmit={(event) => void save(event)} className="grid gap-3 border-t pt-4 sm:grid-cols-2" aria-label={editing > 0 ? t('settings.edit') : t('settings.add')}>
      <Field label={t('settings.name')}>{(props) => <Input {...props} value={name} required autoComplete="off" onChange={(event) => setName(event.target.value)} />}</Field>
      {kind === 'opportunity-types' && <Field label={t('settings.key')} hint={editing > 0 ? t('settings.keyStable') : undefined}>{(props) => <Input {...props} value={keyValue} required disabled={editing > 0} autoComplete="off" onChange={(event) => setKeyValue(event.target.value)} />}</Field>}
      {kind === 'customer-needs' && <><Field label={t('settings.category')}>{(props) => <Input {...props} value={category} onChange={(event) => setCategory(event.target.value)} />}</Field><Field label={t('settings.averagePrice')}>{(props) => <Input {...props} type="number" min="0" step="0.01" value={averagePrice} onChange={(event) => setAveragePrice(event.target.value)} />}</Field></>}
      <div className="flex gap-2 sm:col-span-2"><Button type="submit" disabled={mutation.isPending}>{t('settings.save')}</Button><Button type="button" variant="outline" onClick={() => setEditing(null)}>{t('common.cancel')}</Button></div>
    </form>}

    <AlertDialog open={archiveTarget !== null} onOpenChange={(open) => { if (!open) setArchiveTarget(null) }}>
      <AlertDialogContent>
        <AlertDialogHeader><AlertDialogTitle>{t('settings.confirmArchiveTitle')}</AlertDialogTitle><AlertDialogDescription>{t('settings.confirmArchiveCatalog', { name: archiveTarget?.name ?? '' })}</AlertDialogDescription></AlertDialogHeader>
        <AlertDialogFooter><AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel><AlertDialogAction variant="destructive" onClick={() => { if (archiveTarget) void setStatus(archiveTarget, 'Archived'); setArchiveTarget(null) }}>{t('settings.archive')}</AlertDialogAction></AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  </Card>
}
