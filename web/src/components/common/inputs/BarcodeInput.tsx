import { ScanLine } from 'lucide-react'
import { useRef, useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

/** Below this, the keystrokes came from a wedge scanner, not from fingers. */
const SCANNER_INTERVAL_MS = 35

export interface BarcodeInputProps extends FieldControlProps {
  /**
   * Fired on every completed read — scanned or typed and confirmed with Enter.
   * `scanned` says which, so a warehouse screen can beep only for real scans.
   */
  onSubmit: (code: string, scanned: boolean) => void
  placeholder?: string
  /** Clears the field after each read, ready for the next item. */
  clearOnSubmit?: boolean
  disabled?: boolean
  className?: string
}

/**
 * A barcode field that knows how it was filled.
 *
 * A wedge scanner is a keyboard: it types the code far faster than a person can
 * and ends with Enter. Measuring the gap between keystrokes is what separates
 * "scanned this box" from "typed this SKU by hand", and only the first should
 * commit a stock movement without a second look.
 */
export function BarcodeInput({
  onSubmit,
  placeholder,
  clearOnSubmit = true,
  disabled,
  className,
  id,
  ...aria
}: BarcodeInputProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('barcodeInput.placeholder')
  const [code, setCode] = useState('')
  const [lastScanned, setLastScanned] = useState<string | null>(null)
  const lastKeyAt = useRef(0)
  const fastKeys = useRef(0)

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    const now = performance.now()
    if (event.key === 'Enter') {
      event.preventDefault()
      const trimmed = code.trim()
      if (trimmed.length === 0) return
      // A scan is a burst: most of its characters arrived back to back.
      const scanned = fastKeys.current >= Math.max(trimmed.length - 2, 3)
      onSubmit(trimmed, scanned)
      setLastScanned(scanned ? trimmed : null)
      fastKeys.current = 0
      if (clearOnSubmit) setCode('')
      return
    }
    if (event.key.length === 1) {
      fastKeys.current = now - lastKeyAt.current < SCANNER_INTERVAL_MS ? fastKeys.current + 1 : 0
    }
    lastKeyAt.current = now
  }

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <div className={CONTROL_SHELL}>
        <span className="text-muted-foreground flex shrink-0 items-center pl-3">
          <ScanLine aria-hidden className="size-4" strokeWidth={1.75} />
        </span>
        <input
          id={id}
          type="text"
          inputMode="text"
          autoComplete="off"
          spellCheck={false}
          value={code}
          placeholder={resolvedPlaceholder}
          disabled={disabled}
          onChange={(event) => setCode(event.target.value)}
          onKeyDown={handleKeyDown}
          className={cn(CONTROL_INPUT, 'tnum pl-2.5 tracking-[0.04em]')}
          {...aria}
        />
      </div>
      <p aria-live="polite" className="text-muted-foreground text-[11.5px]">
        {lastScanned ? (
          <>
            {t('barcodeInput.readFromScanner')}{' '}
            <span className="text-[var(--nx-pos)] font-[550]">{lastScanned}</span>
          </>
        ) : (
          t('barcodeInput.hint')
        )}
      </p>
    </div>
  )
}
