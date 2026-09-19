import { FileText, Image as ImageIcon, Sheet, Upload, X } from 'lucide-react'
import { useRef, useState, type DragEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

const UNITS = ['B', 'KB', 'MB', 'GB'] as const

export function formatBytes(bytes: number): string {
  const exponent = bytes === 0 ? 0 : Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), 3)
  const size = bytes / 1024 ** exponent
  return `${new Intl.NumberFormat('tr-TR', { maximumFractionDigits: exponent === 0 ? 0 : 1 }).format(size)} ${UNITS[exponent]}`
}

function iconFor(file: File) {
  if (file.type.startsWith('image/')) return ImageIcon
  if (/sheet|excel|csv/.test(file.type)) return Sheet
  return FileText
}

export interface FileDropzoneProps extends FieldControlProps {
  value: readonly File[]
  onValueChange: (value: File[]) => void
  /** Same syntax as the input attribute: `.pdf,image/*`. */
  accept?: string
  multiple?: boolean
  /** Bytes. Anything larger is rejected with a reason rather than dropped. */
  maxSize?: number
  hint?: string
  disabled?: boolean
  className?: string
}

/**
 * Drag-and-drop attachments with a click fallback.
 *
 * Rejections are shown, not swallowed: an over-sized file that silently fails
 * to attach is discovered by the customer, not by the person sending the quote.
 */
export function FileDropzone({
  value,
  onValueChange,
  accept,
  multiple = true,
  maxSize,
  hint,
  disabled,
  className,
  id,
  ...aria
}: FileDropzoneProps) {
  const { t } = useTranslation('common')
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [rejected, setRejected] = useState<string[]>([])

  const accepted = (files: FileList | null) => {
    if (!files) return
    const tooBig: string[] = []
    const next = [...files].filter((file) => {
      if (maxSize !== undefined && file.size > maxSize) {
        tooBig.push(file.name)
        return false
      }
      return true
    })
    setRejected(tooBig)
    onValueChange(multiple ? [...value, ...next] : next.slice(0, 1))
  }

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragging(false)
    if (disabled) return
    accepted(event.dataTransfer.files)
  }

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div
        onDragOver={(event) => {
          event.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={handleDrop}
        className={cn(
          'flex flex-col items-center gap-1.5 rounded-lg border border-dashed px-4 py-6 text-center',
          'transition-[background,border-color] duration-[250ms] ease-fluid',
          dragging
            ? 'border-ring bg-accent'
            : 'border-[var(--nx-hairline-strong)] bg-[var(--nx-fill)]',
          disabled && 'pointer-events-none opacity-45',
        )}
      >
        <Upload aria-hidden className="text-muted-foreground size-5" strokeWidth={1.6} />
        <p className="text-[13px]">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className="text-accent-foreground cursor-pointer border-0 bg-transparent p-0 font-[590] underline-offset-4 outline-none hover:underline"
          >
            {t('fileDropzone.chooseFile')}
          </button>{' '}
          <span className="text-muted-foreground">{t('fileDropzone.orDrop')}</span>
        </p>
        {(hint || maxSize) && (
          <p className="text-muted-foreground text-[11.5px]">
            {hint ?? t('fileDropzone.maxSize', { size: formatBytes(maxSize ?? 0) })}
          </p>
        )}
        <input
          ref={inputRef}
          id={id}
          type="file"
          accept={accept}
          multiple={multiple}
          disabled={disabled}
          onChange={(event) => {
            accepted(event.target.files)
            // Re-selecting the same file must fire `change` again.
            event.target.value = ''
          }}
          className="hidden"
          {...aria}
        />
      </div>

      {rejected.length > 0 && (
        <p role="alert" className="text-[11.5px] text-[var(--nx-neg)]">
          {t('fileDropzone.rejected', { files: rejected.join(', ') })}
        </p>
      )}

      {value.length > 0 && (
        <ul className="flex flex-col gap-1">
          {value.map((file) => {
            const Icon = iconFor(file)
            return (
              <li
                key={`${file.name}-${file.size}`}
                className="flex items-center gap-2.5 rounded-sm border border-[var(--nx-hairline)] bg-[var(--nx-fill)] py-1.5 pr-1.5 pl-2.5"
              >
                <Icon aria-hidden className="text-brand-graphic size-4 shrink-0" strokeWidth={1.75} />
                <span className="min-w-0 flex-1 truncate text-[12.5px]">{file.name}</span>
                <span className="text-muted-foreground tnum shrink-0 text-[11.5px]">
                  {formatBytes(file.size)}
                </span>
                <button
                  type="button"
                  aria-label={t('fileDropzone.removeFile', { name: file.name })}
                  onClick={() => onValueChange(value.filter((entry) => entry !== file))}
                  className="text-muted-foreground hover:text-foreground flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent outline-none"
                >
                  <X aria-hidden className="size-3.5" strokeWidth={2} />
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
