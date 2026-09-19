import { cn } from "@/lib/utils"

interface SwitchProps {
  checked: boolean
  onCheckedChange: (checked: boolean) => void
  disabled?: boolean
  id?: string
  "aria-label"?: string
  "aria-labelledby"?: string
  className?: string
}

/**
 * 51×31 platform switch. A real `role="switch"` button rather than a styled
 * checkbox, so the accessible name and the on/off state come from one node.
 * The knob is the only element that moves; it rides the standard spring.
 */
function Switch({ checked, onCheckedChange, disabled, className, ...props }: SwitchProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onCheckedChange(!checked)}
      className={cn(
        "relative h-[31px] w-[51px] shrink-0 cursor-pointer rounded-[var(--nx-r-pill)] border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-0 transition-[background,border-color] duration-[250ms] ease-fluid",
        "aria-checked:border-transparent aria-checked:bg-[image:var(--nx-accent-grad)]",
        "disabled:pointer-events-none disabled:opacity-45",
        className
      )}
      {...props}
    >
      <span
        aria-hidden
        className={cn(
          "absolute top-px left-px size-[27px] rounded-full bg-white shadow-[var(--nx-knob-shadow)] transition-transform duration-[250ms] ease-fluid",
          checked && "translate-x-[20px]"
        )}
      />
    </button>
  )
}

export { Switch }
