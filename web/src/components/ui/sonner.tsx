import { Toaster as Sonner, type ToasterProps } from "sonner"
import { CircleCheckIcon, InfoIcon, TriangleAlertIcon, OctagonXIcon, Loader2Icon } from "lucide-react"

import { useResolvedTheme } from "@/store/useAppStore"

/**
 * Customised: the generated component reads `next-themes`, which this app never
 * mounts, so the toasts were pinned to "system" whatever the user picked. Theme
 * lives in the app store, and is read from there already resolved.
 */
const Toaster = ({ ...props }: ToasterProps) => {
  const theme = useResolvedTheme()

  return (
    <Sonner
      theme={theme}
      className="toaster group"
      richColors
      // Every message stays readable: stacked toasts are laid out one under another, not piled on top of each other.
      expand
      visibleToasts={5}
      gap={10}
      offset={{ top: 76, left: 16 }}
      mobileOffset={{ top: 76, left: 16 }}
      icons={{
        success: (
          <CircleCheckIcon className="size-4" />
        ),
        info: (
          <InfoIcon className="size-4" />
        ),
        warning: (
          <TriangleAlertIcon className="size-4" />
        ),
        error: (
          <OctagonXIcon className="size-4" />
        ),
        loading: (
          <Loader2Icon className="size-4 animate-spin" />
        ),
      }}
      style={
        {
          "--normal-bg": "var(--popover)",
          "--normal-text": "var(--popover-foreground)",
          "--normal-border": "var(--border)",
          "--border-radius": "var(--radius)",
          // `richColors` tints only success/info/warning/error — the same
          // green/blue/amber/red pairs `Badge` and `.nx-pill` use, so a toast
          // and its status badge read as one system. Plain toast() keeps
          // the neutral popover surface above.
          "--success-bg": "var(--nx-st-green-bg)",
          "--success-border": "var(--nx-st-green-bg)",
          "--success-text": "var(--nx-st-green-fg)",
          "--info-bg": "var(--nx-st-blue-bg)",
          "--info-border": "var(--nx-st-blue-bg)",
          "--info-text": "var(--nx-st-blue-fg)",
          "--warning-bg": "var(--nx-st-amber-bg)",
          "--warning-border": "var(--nx-st-amber-bg)",
          "--warning-text": "var(--nx-st-amber-fg)",
          "--error-bg": "var(--nx-st-red-bg)",
          "--error-border": "var(--nx-st-red-bg)",
          "--error-text": "var(--nx-st-red-fg)",
        } as React.CSSProperties
      }
      toastOptions={{
        classNames: {
          toast: "cn-toast",
        },
      }}
      {...props}
    />
  )
}

export { Toaster }
