/**
 * Due dates are calendar dates ("2026-10-05"), not instants. Two classic bugs are avoided here:
 *  - `date.toISOString().slice(0, 10)` converts to UTC first: east of Greenwich, local midnight of Oct 5
 *    is still Oct 4 in UTC, so the wrong day is sent.
 *  - `new Date('2026-10-05')` is parsed as UTC midnight and renders as Oct 4 west of Greenwich (e.g. Mexico).
 */
export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function fromIsoDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function startOfToday(now: Date = new Date()): Date {
  return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}
