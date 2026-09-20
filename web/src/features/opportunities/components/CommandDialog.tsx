import { Loader2 } from 'lucide-react'
import type { FormEventHandler, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import type { Problem } from '../lib/problem'
import { ProblemNotice } from './ProblemNotice'

interface CommandDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  submitLabel: string
  pending: boolean
  destructive?: boolean
  problem: Problem | null
  onReload: () => void
  onSubmit: FormEventHandler<HTMLFormElement>
  children?: ReactNode
}

/** The shape every Opportunity command dialog shares: fields, an inline failure, and a submit that cannot be double-fired. */
export function CommandDialog({
  open,
  onOpenChange,
  title,
  description,
  submitLabel,
  pending,
  destructive,
  problem,
  onReload,
  onSubmit,
  children,
}: CommandDialogProps) {
  const { t } = useTranslation('opportunities')
  return (
    // While a request is in flight the dialog cannot be dismissed: closing it would hide an outcome the user is waiting for.
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent>
        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
          {children}
          {problem && <ProblemNotice problem={problem} onReload={onReload} />}
          <DialogFooter>
            <Button type="button" variant="ghost" disabled={pending} onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" variant={destructive ? 'destructive' : 'default'} disabled={pending}>
              {pending && <Loader2 aria-hidden className="animate-spin" />}
              {submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
