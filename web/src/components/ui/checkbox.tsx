import { Check } from "lucide-react"
import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * 18px checkbox over a real `<input type="checkbox">` — the input keeps the
 * label association, the form value and the platform's keyboard behaviour; the
 * box and tick are painted on top of it.
 */
function Checkbox({ className, ...props }: Omit<React.ComponentProps<"input">, "type">) {
  return (
    <span className="relative inline-flex size-[18px] shrink-0 align-middle">
      <input
        type="checkbox"
        data-slot="checkbox"
        className={cn(
          "peer size-[18px] cursor-pointer appearance-none rounded-[6px] border border-[var(--nx-hairline-strong)] bg-[var(--nx-fill)] outline-none transition-[background,border-color] duration-[250ms] ease-fluid",
          "hover:border-[var(--nx-tint)]",
          "checked:border-transparent checked:bg-[image:var(--nx-accent-grad)]",
          "focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/40",
          "disabled:pointer-events-none disabled:opacity-45",
          className
        )}
        {...props}
      />
      <Check
        aria-hidden
        strokeWidth={3}
        className="pointer-events-none absolute inset-0 m-auto size-3 text-white opacity-0 peer-checked:opacity-100"
      />
    </span>
  )
}

export { Checkbox }
