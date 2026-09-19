import { useState } from 'react'
import type { z } from 'zod'
import { FileDropzone } from '@/components/common/inputs/FileDropzone'
import { Button } from '@/components/ui/button'
import { parseFile, type ParsedFile } from '@/lib/importExport/parseFile'

interface ImportWizardProps<T extends z.ZodType> {
  schema: T
  fields: readonly string[]
  onCommit: (rows: z.output<T>[]) => void
}

export function ImportWizard<T extends z.ZodType>({ schema, fields, onCommit }: ImportWizardProps<T>) {
  const [files, setFiles] = useState<File[]>([])
  const [parsed, setParsed] = useState<ParsedFile | null>(null)
  const [mapping, setMapping] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const read = async (file: File) => {
    try { setParsed(await parseFile(file)); setError(null) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Dosya okunamadı.') }
  }
  const validated = parsed?.rows.map((row, index) => {
    const input = Object.fromEntries(Object.entries(mapping).map(([field, header]) => [field, row[parsed.headers.indexOf(header)] ?? '']))
    return { index: index + 2, result: schema.safeParse(input) }
  }) ?? []
  const valid = validated.flatMap((entry) => entry.result.success ? [entry.result.data] : [])
  return <div className="flex flex-col gap-4">
    {!parsed && <FileDropzone value={files} onValueChange={(next) => { setFiles(next); if (next[0]) void read(next[0]) }} accept=".csv,text/csv" multiple={false} hint="CSV dosyası yükleyin; XLSX sonraki adımdır." />}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {parsed && <><div className="grid gap-3 sm:grid-cols-2">{fields.map((field) => <label key={field} className="text-sm font-medium">{field}<select className="mt-1 h-control w-full rounded-md border bg-background px-3" value={mapping[field] ?? ''} onChange={(event) => setMapping((current) => ({ ...current, [field]: event.target.value }))}><option value="">Eşleme yok</option>{parsed.headers.map((header) => <option key={header} value={header}>{header}</option>)}</select></label>)}</div>
      <p className="text-sm text-muted-foreground">{validated.length} satırdan {valid.length} tanesi doğrulandı.</p>
      {validated.flatMap((entry) => entry.result.success ? [] : [entry]).slice(0, 5).map((entry) => <p key={entry.index} className="text-sm text-destructive">Satır {entry.index}: {entry.result.error!.issues.map((issue) => issue.message).join(', ')}</p>)}
      <div className="flex gap-2"><Button onClick={() => onCommit(valid)} disabled={valid.length === 0}>Geçerli satırları içe aktar</Button><Button variant="secondary" onClick={() => { setParsed(null); setFiles([]); setMapping({}) }}>Baştan başla</Button></div>
    </>}
  </div>
}
