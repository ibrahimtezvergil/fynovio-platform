import * as React from "react"
import { Input as InputPrimitive } from "@base-ui/react/input"

import { cn } from "@/lib/utils"

function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <InputPrimitive
      type={type}
      data-slot="input"
      className={cn(
        "h-control w-full min-w-0 rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-[13px] text-base text-foreground outline-none transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid file:inline-flex file:h-6 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-[var(--nx-label-3)] hover:border-[var(--nx-hairline-strong)] focus-visible:border-ring focus-visible:bg-[var(--nx-surface)] focus-visible:shadow-[0_0_0_4px_var(--nx-tint-fill)] disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-45 aria-invalid:border-destructive aria-invalid:focus-visible:shadow-[0_0_0_4px_var(--nx-st-red-bg)] md:text-[13.5px]",
        className
      )}
      {...props}
    />
  )
}

export { Input }
