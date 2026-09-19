import { Outlet } from 'react-router-dom'

/** Centred single-column shell for the public pages, over the ambient wash. */
export function AuthLayout() {
  return (
    <div className="bg-background relative isolate flex min-h-screen items-center justify-center p-6">
      <div aria-hidden className="nx-ambient" />
      <div className="relative z-[1] w-full max-w-sm">
        <div className="mb-6 flex items-center justify-center gap-2.5">
          <span
            aria-hidden
            className="flex size-8 items-center justify-center rounded-[var(--nx-r-ctl-sm)] bg-[image:var(--nx-accent-grad)] text-[15px] font-[650] tracking-[-0.02em] text-white shadow-[var(--nx-accent-glow),inset_0_1px_0_rgb(255_255_255/0.35)]"
          >
            F
          </span>
          <span className="font-heading text-[17px] font-semibold tracking-[-0.026em]">
            Fynovio
          </span>
        </div>
        <Outlet />
      </div>
    </div>
  )
}
