/** The four calendar views this app exposes, in toolbar order. */
export const CALENDAR_VIEWS = ['dayGridMonth', 'timeGridWeek', 'timeGridDay', 'listWeek'] as const
export type CalendarView = (typeof CALENDAR_VIEWS)[number]
