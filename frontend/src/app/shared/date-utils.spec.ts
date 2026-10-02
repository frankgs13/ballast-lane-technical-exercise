import { fromIsoDate, startOfToday, toIsoDate } from './date-utils';

describe('date-utils', () => {
  it('formats a local date as yyyy-MM-dd without shifting to UTC', () => {
    // Local midnight: toISOString() would produce the previous day in UTC+ time zones.
    expect(toIsoDate(new Date(2026, 9, 5))).toBe('2026-10-05');
    expect(toIsoDate(new Date(2026, 0, 1, 23, 59))).toBe('2026-01-01');
  });

  it('parses yyyy-MM-dd as a local calendar date', () => {
    const date = fromIsoDate('2026-10-05');

    expect(date.getFullYear()).toBe(2026);
    expect(date.getMonth()).toBe(9);
    expect(date.getDate()).toBe(5);
    expect(date.getHours()).toBe(0);
  });

  it('round-trips', () => {
    expect(toIsoDate(fromIsoDate('2028-02-29'))).toBe('2028-02-29');
  });

  it('startOfToday drops the time component', () => {
    const today = startOfToday(new Date(2026, 9, 2, 18, 45, 12));

    expect(toIsoDate(today)).toBe('2026-10-02');
    expect(today.getHours()).toBe(0);
    expect(today.getMinutes()).toBe(0);
  });
});
