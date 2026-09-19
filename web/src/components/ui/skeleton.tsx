import type * as React from "react"

import { cn } from "@/lib/utils"

/**
 * A block that holds a piece of pending content's space. Give it the size of
 * the thing it stands in for — a skeleton that does not match its final
 * layout costs the reader a second reflow when the data lands.
 */
function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="skeleton"
      aria-hidden
      className={cn("nx-skeleton", className)}
      {...props}
    />
  )
}

export { Skeleton }
