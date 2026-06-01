// ---------------------------------------------------------------------------
// features/dashboard/deadlines.ts
// Filing-deadline helpers for the dashboard KPI + deadline list. The statutory
// non-audit ITR due date is 31 July of the assessment year; the belated/revised
// window runs to 31 December. Derived from the AY code ("AY2025-26" → due 31 Jul
// 2025). Pure + UTC-safe so it renders identically on server and client.
// ---------------------------------------------------------------------------

export interface DeadlineInfo {
  /** The ITR filing due date (non-audit) for the assessment year. */
  dueDate: Date;
  /** The belated/revised cut-off (31 Dec of the assessment year). */
  belatedDate: Date;
  /** Whole days from `now` to dueDate (negative once past). */
  daysToDue: number;
  /** True once the 31 Jul due date has passed. */
  isPastDue: boolean;
  /** True once even the belated window has closed. */
  isClosed: boolean;
}

/** Parse the start calendar year from an AY code: "AY2025-26" → 2025. */
export function assessmentYearStart(code: string): number | null {
  const m = /^AY(\d{4})-\d{2}$/.exec(code.trim());
  return m ? Number(m[1]) : null;
}

const MS_PER_DAY = 24 * 60 * 60 * 1000;

/** Compute deadline info for an AY relative to `now` (defaults to current date). */
export function deadlineFor(ayCode: string, now: Date = new Date()): DeadlineInfo | null {
  const year = assessmentYearStart(ayCode);
  if (year === null) return null;

  // Use UTC midnight to keep the day-count stable regardless of timezone.
  const dueDate = new Date(Date.UTC(year, 6, 31)); // 31 Jul (month index 6)
  const belatedDate = new Date(Date.UTC(year, 11, 31)); // 31 Dec
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());

  const daysToDue = Math.round((dueDate.getTime() - today) / MS_PER_DAY);

  return {
    dueDate,
    belatedDate,
    daysToDue,
    isPastDue: today > dueDate.getTime(),
    isClosed: today > belatedDate.getTime(),
  };
}
