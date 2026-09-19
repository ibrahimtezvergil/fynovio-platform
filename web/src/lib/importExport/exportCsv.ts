export interface CsvColumn<T> { key: keyof T; header: string }

const escape = (value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`

export function rowsToCsv<T extends object>(rows: readonly T[], columns: readonly CsvColumn<T>[]): string {
  return [columns.map((column) => escape(column.header)).join(','), ...rows.map((row) => columns.map((column) => escape(row[column.key])).join(','))].join('\n')
}

export function exportRowsToCsv<T extends object>(rows: readonly T[], columns: readonly CsvColumn<T>[], filename = 'export.csv') {
  const csv = rowsToCsv(rows, columns)
  const link = document.createElement('a')
  link.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  link.download = filename
  link.click()
  URL.revokeObjectURL(link.href)
  return csv
}
