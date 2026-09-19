export interface ParsedFile {
  headers: string[]
  rows: string[][]
}

/** Small CSV parser for client-side imports; XLSX needs a server-approved parser later. */
export async function parseFile(file: File): Promise<ParsedFile> {
  if (!file.name.toLowerCase().endsWith('.csv') && file.type !== 'text/csv') {
    throw new Error('Yalnızca CSV dosyaları desteklenir.')
  }
  const cells: string[][] = [[]]
  let quoted = false
  const text = (await file.text()).replace(/^\uFEFF/, '')
  for (let index = 0; index < text.length; index += 1) {
    const character = text[index]!
    if (character === '"' && text[index + 1] === '"' && quoted) {
      cells.at(-1)!.push('"')
      index += 1
    } else if (character === '"') quoted = !quoted
    else if (character === ',' && !quoted) cells.at(-1)!.push('\u0000')
    else if ((character === '\n' || character === '\r') && !quoted) {
      if (character === '\r' && text[index + 1] === '\n') index += 1
      cells.push([])
    } else cells.at(-1)!.push(character)
  }
  const rows = cells.map((row) => row.join('').split('\u0000').map((cell) => cell.trim())).filter((row) => row.some(Boolean))
  return { headers: rows[0] ?? [], rows: rows.slice(1) }
}
