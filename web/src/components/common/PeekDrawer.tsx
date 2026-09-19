import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'

interface PeekDrawerProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  position: string
  onPrevious: () => void
  onNext: () => void
  hasPrevious: boolean
  hasNext: boolean
  children: ReactNode
}

/**
 * A contextual record peek: navigation stays in the list that launched it,
 * rather than pretending a full detail route exists. Domain content is passed
 * as children so this remains a shared overlay primitive.
 */
export function PeekDrawer({
  open, onOpenChange, title, description, position, onPrevious, onNext, hasPrevious, hasNext, children,
}: PeekDrawerProps) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="gap-0 sm:max-w-xl">
        <SheetHeader className="gap-3">
          <div className="flex items-center gap-1">
            <Button variant="ghost" size="icon-sm" aria-label="Önceki kayıt" disabled={!hasPrevious} onClick={onPrevious}>
              <ChevronLeft aria-hidden strokeWidth={1.8} />
            </Button>
            <Button variant="ghost" size="icon-sm" aria-label="Sonraki kayıt" disabled={!hasNext} onClick={onNext}>
              <ChevronRight aria-hidden strokeWidth={1.8} />
            </Button>
            <span className="text-muted-foreground tnum ml-1 text-[11.5px]">{position}</span>
          </div>
          <div>
            <SheetTitle>{title}</SheetTitle>
            <SheetDescription>{description}</SheetDescription>
          </div>
        </SheetHeader>
        <div className="flex-1 overflow-y-auto border-t border-[var(--nx-hairline)] px-5 py-4">{children}</div>
      </SheetContent>
    </Sheet>
  )
}
