import { StatusBadge as Pill, type StatusRegistry } from '@/components/common/StatusBadge'
import type { EmployeeStatus } from '@/features/demo-tables/data/employees'

const STATUS_META: StatusRegistry<EmployeeStatus> = {
  active: { label: 'Active', tone: 'green' },
  invited: { label: 'Invited', tone: 'blue' },
  suspended: { label: 'Suspended', tone: 'amber' },
}

/** Dot plus label, never colour alone — survives greyscale and colour blindness. */
export function StatusBadge({ status }: { status: EmployeeStatus }) {
  return <Pill {...STATUS_META[status]} />
}
