import { Eraser } from 'lucide-react'
import { useEffect, useRef, useState, type PointerEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import type { FieldControlProps } from './types'

export interface SignaturePadProps extends FieldControlProps {
  /** A PNG data URL, or `null` while the pad is empty. */
  value: string | null
  onValueChange: (value: string | null) => void
  height?: number
  disabled?: boolean
  className?: string
}

/**
 * A signature drawn with a finger or a stylus — the delivery note's proof of
 * handover.
 *
 * The canvas is backed at device resolution and stroked in a fixed ink colour
 * rather than the theme's: a signature captured in dark mode has to stay legible
 * on a white printed page.
 */
export function SignaturePad({
  value,
  onValueChange,
  height = 160,
  disabled,
  className,
  id,
  ...aria
}: SignaturePadProps) {
  const { t } = useTranslation('common')
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const drawing = useRef(false)
  const [dirty, setDirty] = useState(value !== null)

  // Back the canvas at device resolution once it has a measured width, or the
  // stroke is soft on every screen that isn't 1×.
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ratio = window.devicePixelRatio || 1
    const { width } = canvas.getBoundingClientRect()
    canvas.width = Math.round(width * ratio)
    canvas.height = Math.round(height * ratio)
    const context = canvas.getContext('2d')
    if (!context) return
    context.scale(ratio, ratio)
    context.lineWidth = 2
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.strokeStyle = '#101320'
  }, [height])

  const pointAt = (event: PointerEvent<HTMLCanvasElement>) => {
    const rect = event.currentTarget.getBoundingClientRect()
    return { x: event.clientX - rect.left, y: event.clientY - rect.top }
  }

  const start = (event: PointerEvent<HTMLCanvasElement>) => {
    if (disabled) return
    const context = canvasRef.current?.getContext('2d')
    if (!context) return
    event.currentTarget.setPointerCapture(event.pointerId)
    drawing.current = true
    const { x, y } = pointAt(event)
    context.beginPath()
    context.moveTo(x, y)
  }

  const move = (event: PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return
    const context = canvasRef.current?.getContext('2d')
    if (!context) return
    const { x, y } = pointAt(event)
    context.lineTo(x, y)
    context.stroke()
    setDirty(true)
  }

  const end = () => {
    if (!drawing.current) return
    drawing.current = false
    const canvas = canvasRef.current
    if (canvas) onValueChange(canvas.toDataURL('image/png'))
  }

  const clear = () => {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return
    context.clearRect(0, 0, canvas.width, canvas.height)
    setDirty(false)
    onValueChange(null)
  }

  return (
    <div className={cn('flex flex-col gap-2', className)} {...aria}>
      <div
        className={cn(
          'relative overflow-hidden rounded-md border border-[var(--nx-hairline)] bg-white',
          disabled && 'pointer-events-none opacity-45',
        )}
      >
        <canvas
          ref={canvasRef}
          id={id}
          role="img"
          aria-label={t('signaturePad.area')}
          style={{ height }}
          onPointerDown={start}
          onPointerMove={move}
          onPointerUp={end}
          onPointerCancel={end}
          className="block w-full touch-none"
        />
        {!dirty && (
          <span className="pointer-events-none absolute inset-0 flex items-center justify-center text-[12.5px] text-[#8a90a6]">
            {t('signaturePad.hint')}
          </span>
        )}
        <span
          aria-hidden
          className="pointer-events-none absolute inset-x-6 bottom-6 border-b border-dashed border-[#c8ccdb]"
        />
      </div>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        disabled={disabled || !dirty}
        onClick={clear}
        className="self-start"
      >
        <Eraser aria-hidden />
        {t('signaturePad.clear')}
      </Button>
    </div>
  )
}
