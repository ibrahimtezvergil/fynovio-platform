import { Button as ButtonPrimitive } from "@base-ui/react/button"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

/**
 * `default` is the accent gradient — the only filled action on a screen, and
 * the only control that casts (the artboard's accent glow). `secondary` and
 * `outline` are translucent material held in the plane by an edge highlight.
 * Every state change rides the same spring: 0.25s cubic-bezier(.16,1,.3,1).
 */
const buttonVariants = cva(
  [
    "group/button inline-flex shrink-0 items-center justify-center gap-[7px] rounded-md border border-transparent",
    "text-[13.5px] font-[590] tracking-[-0.01em] whitespace-nowrap select-none outline-none",
    "transition-[background,border-color,color,filter,box-shadow,transform] duration-[250ms] ease-fluid",
    "active:scale-[0.97] active:duration-[120ms]",
    "focus-visible:ring-3 focus-visible:ring-ring/40 focus-visible:border-ring",
    "disabled:pointer-events-none disabled:opacity-40",
    "aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20",
    "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  ].join(" "),
  {
    variants: {
      variant: {
        default:
          "bg-[image:var(--nx-accent-grad)] text-[var(--nx-on-accent)] border-white/18 shadow-[var(--nx-accent-glow),inset_0_1px_0_rgb(255_255_255/0.3)] hover:brightness-[1.08]",
        outline:
          "bg-[var(--nx-glass-2)] backdrop-blur-[24px] backdrop-saturate-[170%] border-[var(--nx-hairline)] text-foreground shadow-[inset_0_1px_0_var(--nx-specular)] hover:bg-[var(--nx-fill-hover)] hover:border-[var(--nx-hairline-strong)] aria-expanded:bg-[var(--nx-fill-active)]",
        secondary:
          "bg-[var(--nx-glass-2)] backdrop-blur-[24px] backdrop-saturate-[170%] border-[var(--nx-hairline)] text-foreground shadow-[inset_0_1px_0_var(--nx-specular)] hover:bg-[var(--nx-fill-hover)] hover:border-[var(--nx-hairline-strong)] aria-expanded:bg-[var(--nx-fill-active)]",
        tinted:
          "bg-accent text-accent-foreground hover:bg-[var(--nx-tint-fill-hover)]",
        ghost:
          "text-muted-foreground hover:bg-[var(--nx-fill-hover)] hover:text-foreground aria-expanded:bg-[var(--nx-fill-active)] aria-expanded:text-foreground",
        destructive:
          "bg-[var(--nx-st-red-bg)] text-[var(--nx-st-red-fg)] hover:brightness-105 focus-visible:ring-destructive/30",
        link: "text-accent-foreground underline-offset-4 hover:underline",
      },
      size: {
        default: "h-control px-[15px]",
        xs: "h-6 gap-1 rounded-sm px-2 text-[11.5px] [&_svg:not([class*='size-'])]:size-3",
        sm: "h-[30px] rounded-sm px-[11px] text-[12.5px] [&_svg:not([class*='size-'])]:size-3.5",
        lg: "h-[46px] px-5 text-[15px]",
        icon: "size-control p-0",
        "icon-xs": "size-6 rounded-sm p-0 [&_svg:not([class*='size-'])]:size-3",
        "icon-sm": "size-[30px] rounded-sm p-0 [&_svg:not([class*='size-'])]:size-3.5",
        "icon-lg": "size-[46px] p-0",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

function Button({
  className,
  variant = "default",
  size = "default",
  ...props
}: ButtonPrimitive.Props & VariantProps<typeof buttonVariants>) {
  return (
    <ButtonPrimitive
      data-slot="button"
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  )
}

export { Button, buttonVariants }
