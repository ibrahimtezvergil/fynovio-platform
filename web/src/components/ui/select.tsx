import { ChevronDown } from "lucide-react"
import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * A native `<select>` wearing the input's material.
 *
 * Native on purpose: it inherits the platform's own picker, keyboard handling
 * and mobile sheet, which no scripted listbox reproduces for free. The chevron
 * is decoration layered over the suppressed UA arrow.
 */
function Select({ className, children, ...props }: React.ComponentProps<"select">) {
  return (
    <span className="relative flex items-center">
      <select
        data-slot="select"
        className={cn(
          "h-control w-full cursor-pointer appearance-none rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] pr-[34px] pl-[13px] text-[13.5px] text-foreground outline-none transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid hover:border-[var(--nx-hairline-strong)] focus-visible:border-ring focus-visible:bg-[var(--nx-surface)] focus-visible:shadow-[0_0_0_4px_var(--nx-tint-fill)] disabled:pointer-events-none disabled:opacity-45",
          className
        )}
        {...props}
      >
        {children}
      </select>
      <ChevronDown
        aria-hidden
        strokeWidth={1.7}
        className="text-muted-foreground pointer-events-none absolute right-3 size-4"
      />
    </span>
  )
}

export { Select }
