import { mergeProps } from "@base-ui/react/merge-props"
import { useRender } from "@base-ui/react/use-render"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const badgeVariants = cva(
  "group/badge inline-flex h-6 w-fit shrink-0 items-center justify-center gap-1.5 overflow-hidden rounded-full border border-transparent px-2.5 text-xs font-[590] tracking-[-0.005em] whitespace-nowrap transition-all duration-[250ms] ease-fluid focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/40 has-data-[icon=inline-end]:pr-2 has-data-[icon=inline-start]:pl-2 aria-invalid:border-destructive aria-invalid:ring-destructive/20 [&>svg]:pointer-events-none [&>svg]:size-3.5!",
  {
    variants: {
      variant: {
        default: "bg-accent text-accent-foreground [a]:hover:bg-[var(--nx-tint-fill-hover)]",
        secondary:
          "bg-[var(--nx-st-gray-bg)] text-[var(--nx-st-gray-fg)] [a]:hover:brightness-105",
        destructive:
          "bg-[var(--nx-st-red-bg)] text-[var(--nx-st-red-fg)] focus-visible:ring-destructive/30 [a]:hover:brightness-105",
        success:
          "bg-[var(--nx-st-green-bg)] text-[var(--nx-st-green-fg)] [a]:hover:brightness-105",
        warning:
          "bg-[var(--nx-st-amber-bg)] text-[var(--nx-st-amber-fg)] [a]:hover:brightness-105",
        info: "bg-[var(--nx-st-blue-bg)] text-[var(--nx-st-blue-fg)] [a]:hover:brightness-105",
        outline:
          "text-muted-foreground shadow-[inset_0_0_0_1px_var(--nx-hairline-strong)] [a]:hover:bg-[var(--nx-fill-hover)]",
        ghost:
          "text-muted-foreground hover:bg-[var(--nx-fill-hover)] hover:text-foreground",
        link: "text-accent-foreground underline-offset-4 hover:underline",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

function Badge({
  className,
  variant = "default",
  render,
  ...props
}: useRender.ComponentProps<"span"> & VariantProps<typeof badgeVariants>) {
  return useRender({
    defaultTagName: "span",
    props: mergeProps<"span">(
      {
        className: cn(badgeVariants({ variant }), className),
      },
      props
    ),
    render,
    state: {
      slot: "badge",
      variant,
    },
  })
}

export { Badge, badgeVariants }
