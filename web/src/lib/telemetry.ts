/**
 * The application-level telemetry seam. A vendor integration belongs here so
 * callers never need to know where events are ultimately sent.
 */
export function track(event: string, props?: Record<string, unknown>): void {
  console.debug('[telemetry]', event, props)
}
