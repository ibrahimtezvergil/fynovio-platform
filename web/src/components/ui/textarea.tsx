import * as React from "react"

import { cn } from "@/lib/utils"

/** Same material as `Input`, sized for prose and vertically resizable only. */
function Textarea({ className, ...props }: React.ComponentProps<"textarea">) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        "min-h-[92px] w-full resize-y rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] px-[13px] py-2.5 text-[13.5px] leading-[1.5] text-foreground outline-none transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid placeholder:text-[var(--nx-label-3)] hover:border-[var(--nx-hairline-strong)] focus-visible:border-ring focus-visible:bg-[var(--nx-surface)] focus-visible:shadow-[0_0_0_4px_var(--nx-tint-fill)] disabled:pointer-events-none disabled:opacity-45 aria-invalid:border-destructive aria-invalid:focus-visible:shadow-[0_0_0_4px_var(--nx-st-red-bg)]",
        className
      )}
      {...props}
    />
  )
}

export { Textarea }
