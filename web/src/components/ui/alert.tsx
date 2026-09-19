import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"

/**
 * Tones mirror `Badge` and `.nx-pill` — the same green/amber/blue/red pairs,
 * just spent on a bordered banner instead of a chip. `default` stays neutral
 * (bg-card) since a page can only afford one loud, tinted surface at a time.
 */
const alertVariants = cva(
  "group/alert relative grid w-full gap-0.5 rounded-[var(--nx-r-ctl)] border border-transparent px-4 py-3.5 text-left text-sm has-data-[slot=alert-action]:relative has-data-[slot=alert-action]:pr-18 has-[>svg]:grid-cols-[auto_1fr] has-[>svg]:gap-x-3 *:[svg]:row-span-2 *:[svg]:size-4.5 *:[svg]:translate-y-0.5 *:[svg]:text-current *:[svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "border-[var(--nx-hairline)] bg-card text-card-foreground",
        destructive: "bg-[var(--nx-st-red-bg)] text-[var(--nx-st-red-fg)]",
        success: "bg-[var(--nx-st-green-bg)] text-[var(--nx-st-green-fg)]",
        warning: "bg-[var(--nx-st-amber-bg)] text-[var(--nx-st-amber-fg)]",
        info: "bg-[var(--nx-st-blue-bg)] text-[var(--nx-st-blue-fg)]",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

function Alert({
  className,
  variant,
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof alertVariants>) {
  return (
    <div
      data-slot="alert"
      role="alert"
      className={cn(alertVariants({ variant }), className)}
      {...props}
    />
  )
}

function AlertTitle({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-title"
      className={cn(
        "text-[13px] font-[590] group-has-[>svg]/alert:col-start-2 [&_a]:underline [&_a]:underline-offset-3 [&_a]:hover:opacity-80",
        className
      )}
      {...props}
    />
  )
}

function AlertDescription({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-description"
      className={cn(
        "text-[12.5px] leading-[1.5] text-balance opacity-85 group-has-[>svg]/alert:col-start-2 md:text-pretty [&_a]:underline [&_a]:underline-offset-3 [&_p:not(:last-child)]:mb-4",
        className
      )}
      {...props}
    />
  )
}

function AlertAction({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-action"
      className={cn("absolute top-2.5 right-2.5", className)}
      {...props}
    />
  )
}

export { Alert, AlertTitle, AlertDescription, AlertAction }
