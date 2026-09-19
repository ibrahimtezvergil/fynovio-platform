import { tr } from 'date-fns/locale'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { DayPicker, type DayPickerProps } from 'react-day-picker'
import { cn } from '@/lib/utils'

const NAV_BUTTON = [
  'flex size-7 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent',
  'text-muted-foreground outline-none transition-colors duration-[150ms] ease-fluid',
  'hover:bg-[var(--nx-fill-hover)] hover:text-foreground disabled:opacity-30',
].join(' ')

const DAY_BUTTON = [
  'flex size-9 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent',
  'text-[13px] tabular-nums text-foreground outline-none',
  'transition-[background,color] duration-[150ms] ease-fluid',
  'hover:bg-[var(--nx-fill-hover)] focus-visible:ring-3 focus-visible:ring-ring/40',
].join(' ')

/**
 * The month grid, wearing the app's material. Only the class map is ours —
 * keyboard navigation, range logic, week structure and the Turkish month and
 * weekday names all come from the library and its `date-fns` locale.
 */
export function Calendar({ className, classNames, ...props }: DayPickerProps) {
  return (
    <DayPicker
      locale={tr}
      showOutsideDays
      className={cn('relative', className)}
      components={{
        Chevron: ({ orientation }) =>
          orientation === 'left' ? (
            <ChevronLeft aria-hidden className="size-4" strokeWidth={1.75} />
          ) : (
            <ChevronRight aria-hidden className="size-4" strokeWidth={1.75} />
          ),
      }}
      classNames={{
        months: 'flex flex-col gap-5 sm:flex-row',
        month: 'flex flex-col gap-2',
        month_caption: 'flex h-8 items-center justify-center',
        caption_label: 'text-[13.5px] font-[590] capitalize',
        nav: 'absolute inset-x-0 top-0 z-1 flex h-8 items-center justify-between',
        button_previous: NAV_BUTTON,
        button_next: NAV_BUTTON,
        month_grid: 'w-full border-collapse',
        weekday:
          'h-8 w-9 text-[10.5px] font-[650] tracking-[0.05em] text-[var(--nx-label-3)] uppercase',
        day: 'p-0 text-center align-middle',
        day_button: DAY_BUTTON,
        today: '[&>button]:font-[650] [&>button]:text-accent-foreground',
        outside: '[&>button]:text-[var(--nx-label-3)] [&>button]:opacity-60',
        disabled: 'pointer-events-none [&>button]:opacity-30',
        hidden: 'invisible',
        selected:
          '[&>button]:bg-[image:var(--nx-accent-grad)] [&>button]:font-[590] [&>button]:text-white',
        range_middle: 'bg-accent [&>button]:rounded-none [&>button]:bg-none! [&>button]:text-accent-foreground',
        range_start: 'rounded-l-sm bg-accent',
        range_end: 'rounded-r-sm bg-accent',
        ...classNames,
      }}
      {...props}
    />
  )
}
