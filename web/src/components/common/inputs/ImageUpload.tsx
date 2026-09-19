import { ImagePlus, RotateCcw, Trash2, ZoomIn } from 'lucide-react'
import { useEffect, useRef, useState, type PointerEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { SliderField } from './SliderField'
import { formatBytes } from './FileDropzone'
import type { FieldControlProps } from './types'

/** Fixed so the crop maths never has to wait for a layout measurement. */
const VIEWPORT_WIDTH = 260

interface Natural {
  width: number
  height: number
}

export interface ImageUploadProps extends FieldControlProps {
  /** The cropped result as a data URL, or `null`. */
  value: string | null
  onValueChange: (value: string | null) => void
  /** Output width in px; the height follows `aspect`. */
  size?: number
  /** Viewport shape — `1` for an avatar, `16 / 9` for a cover. */
  aspect?: number
  /** Circular mask over the same square crop. */
  round?: boolean
  maxSize?: number
  disabled?: boolean
  className?: string
}

/**
 * Pick an image, frame it, keep the frame.
 *
 * Cropping happens here rather than on the server because the framing is a
 * decision, not a transform: only the person uploading knows whether the logo
 * should sit tight or breathe. What leaves is already the final image, so
 * nothing downstream has to store an original plus a crop rectangle.
 */
export function ImageUpload({
  value,
  onValueChange,
  size = 256,
  aspect = 1,
  round = false,
  maxSize,
  disabled,
  className,
  id,
  ...aria
}: ImageUploadProps) {
  const { t } = useTranslation('common')
  const inputRef = useRef<HTMLInputElement>(null)
  const imageRef = useRef<HTMLImageElement>(null)
  const dragFrom = useRef<{ x: number; y: number } | null>(null)

  const [source, setSource] = useState<string | null>(null)
  const [natural, setNatural] = useState<Natural | null>(null)
  const [zoom, setZoom] = useState(1)
  const [offset, setOffset] = useState({ x: 0, y: 0 })
  const [dragging, setDragging] = useState(false)
  const [rejected, setRejected] = useState<string | null>(null)

  const viewportHeight = Math.round(VIEWPORT_WIDTH / aspect)

  // The object URL outlives the render that made it, so it is revoked when the
  // source changes or the component goes away — not in the picker's handler.
  useEffect(() => {
    if (!source) return
    return () => URL.revokeObjectURL(source)
  }, [source])

  const baseScale = natural
    ? Math.max(VIEWPORT_WIDTH / natural.width, viewportHeight / natural.height)
    : 1
  const scale = baseScale * zoom
  const drawnWidth = (natural?.width ?? 0) * scale
  const drawnHeight = (natural?.height ?? 0) * scale
  const limitX = Math.max((drawnWidth - VIEWPORT_WIDTH) / 2, 0)
  const limitY = Math.max((drawnHeight - viewportHeight) / 2, 0)

  const clamp = (next: { x: number; y: number }) => ({
    x: Math.min(Math.max(next.x, -limitX), limitX),
    y: Math.min(Math.max(next.y, -limitY), limitY),
  })

  const pick = (files: FileList | null) => {
    const file = files?.[0]
    if (!file) return
    if (maxSize !== undefined && file.size > maxSize) {
      setRejected(`${file.name} (${formatBytes(file.size)})`)
      return
    }
    setRejected(null)
    setSource(URL.createObjectURL(file))
    setNatural(null)
    setZoom(1)
    setOffset({ x: 0, y: 0 })
  }

  const startDrag = (event: PointerEvent<HTMLDivElement>) => {
    if (!source) return
    event.currentTarget.setPointerCapture(event.pointerId)
    dragFrom.current = { x: event.clientX - offset.x, y: event.clientY - offset.y }
    setDragging(true)
  }

  const drag = (event: PointerEvent<HTMLDivElement>) => {
    if (!dragFrom.current) return
    setOffset(
      clamp({ x: event.clientX - dragFrom.current.x, y: event.clientY - dragFrom.current.y }),
    )
  }

  const endDrag = () => {
    dragFrom.current = null
    setDragging(false)
  }

  /** Maps the on-screen frame back onto source pixels and paints it out. */
  const commit = () => {
    const image = imageRef.current
    if (!image || !natural) return
    const originX = VIEWPORT_WIDTH / 2 + offset.x - drawnWidth / 2
    const originY = viewportHeight / 2 + offset.y - drawnHeight / 2

    const canvas = document.createElement('canvas')
    canvas.width = size
    canvas.height = Math.round(size / aspect)
    const context = canvas.getContext('2d')
    if (!context) return
    context.drawImage(
      image,
      -originX / scale,
      -originY / scale,
      VIEWPORT_WIDTH / scale,
      viewportHeight / scale,
      0,
      0,
      canvas.width,
      canvas.height,
    )
    onValueChange(canvas.toDataURL('image/png'))
    setSource(null)
    setNatural(null)
  }

  return (
    <div className={cn('flex flex-col gap-3', className)} {...aria}>
      <input
        ref={inputRef}
        id={id}
        type="file"
        accept="image/*"
        disabled={disabled}
        onChange={(event) => {
          pick(event.target.files)
          event.target.value = ''
        }}
        className="hidden"
      />

      {source ? (
        <div className="flex flex-col gap-3">
          <div
            onPointerDown={startDrag}
            onPointerMove={drag}
            onPointerUp={endDrag}
            onPointerCancel={endDrag}
            style={{ width: VIEWPORT_WIDTH, height: viewportHeight }}
            className={cn(
              'relative touch-none overflow-hidden border border-[var(--nx-hairline)] bg-[var(--nx-fill)]',
              dragging ? 'cursor-grabbing' : 'cursor-grab',
              round ? 'rounded-full' : 'rounded-lg',
            )}
          >
            <img
              ref={imageRef}
              src={source}
              alt=""
              draggable={false}
              onLoad={(event) =>
                setNatural({
                  width: event.currentTarget.naturalWidth,
                  height: event.currentTarget.naturalHeight,
                })
              }
              style={{
                width: drawnWidth || undefined,
                height: drawnHeight || undefined,
                left: VIEWPORT_WIDTH / 2 + offset.x - drawnWidth / 2,
                top: viewportHeight / 2 + offset.y - drawnHeight / 2,
              }}
              className="absolute max-w-none select-none"
            />
          </div>

          <div className="flex max-w-[260px] items-center gap-2">
            <ZoomIn aria-hidden className="text-muted-foreground size-4" strokeWidth={1.75} />
            <SliderField
              aria-label={t('imageUpload.zoom')}
              value={zoom}
              onValueChange={(next) => {
                setZoom(next)
                setOffset((current) => clamp(current))
              }}
              min={1}
              max={3}
              step={0.01}
              format={{ style: 'percent', maximumFractionDigits: 0 }}
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <Button type="button" size="sm" onClick={commit}>
              {t('imageUpload.cropAndUse')}
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => setSource(null)}>
              {t('imageUpload.cancel')}
            </Button>
          </div>
        </div>
      ) : value ? (
        <div className="flex items-center gap-3">
          <img
            src={value}
            alt={t('imageUpload.previewAlt')}
            style={{ width: 88, height: Math.round(88 / aspect) }}
            className={cn(
              'border border-[var(--nx-hairline)] object-cover',
              round ? 'rounded-full' : 'rounded-lg',
            )}
          />
          <div className="flex flex-col gap-2">
            <div className="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                size="sm"
                disabled={disabled}
                onClick={() => inputRef.current?.click()}
              >
                <RotateCcw aria-hidden />
                {t('imageUpload.change')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled}
                onClick={() => onValueChange(null)}
              >
                <Trash2 aria-hidden />
                {t('imageUpload.remove')}
              </Button>
            </div>
            <span className="text-muted-foreground text-[11.5px]">
              {t('imageUpload.croppedTo', { width: size, height: Math.round(size / aspect) })}
            </span>
          </div>
        </div>
      ) : (
        <button
          type="button"
          disabled={disabled}
          onClick={() => inputRef.current?.click()}
          className={cn(
            'flex w-full max-w-[260px] cursor-pointer flex-col items-center gap-1.5 rounded-lg border border-dashed px-4 py-6',
            'border-[var(--nx-hairline-strong)] bg-[var(--nx-fill)] outline-none',
            'transition-[background,border-color] duration-[250ms] ease-fluid hover:border-ring hover:bg-accent',
            'disabled:pointer-events-none disabled:opacity-45',
          )}
        >
          <ImagePlus aria-hidden className="text-muted-foreground size-5" strokeWidth={1.6} />
          <span className="text-[13px] font-[550]">{t('imageUpload.chooseImage')}</span>
          {maxSize !== undefined && (
            <span className="text-muted-foreground text-[11.5px]">
              {t('imageUpload.maxSize', { size: formatBytes(maxSize) })}
            </span>
          )}
        </button>
      )}

      {rejected && (
        <p role="alert" className="text-[11.5px] text-[var(--nx-neg)]">
          {t('imageUpload.rejected', { file: rejected })}
        </p>
      )}
    </div>
  )
}
