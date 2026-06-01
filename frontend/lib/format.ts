// ---------------------------------------------------------------------------
// format.ts — INR currency + date formatting (Indian lakh/crore grouping).
// Locale en-IN for both EN and HI (grouping is identical; only words differ).
// Money arrives from the API as string decimals; parse safely here.
// ---------------------------------------------------------------------------

const inrFormatter = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

const inrPaiseFormatter = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const groupFormatter = new Intl.NumberFormat('en-IN', {
  maximumFractionDigits: 2,
});

/** Coerce a money value (string decimal from API, or number) to a finite number. */
export function toNumber(value: string | number | null | undefined): number {
  if (value === null || value === undefined || value === '') return 0;
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : 0;
}

/**
 * Format money as Indian-grouped INR, e.g. 1234567 -> "₹12,34,567".
 * Pass { paise: true } to always show two decimals (₹12,34,567.00).
 */
export function formatInr(
  value: string | number | null | undefined,
  opts?: { paise?: boolean },
): string {
  const n = toNumber(value);
  return opts?.paise ? inrPaiseFormatter.format(n) : inrFormatter.format(n);
}

/** Indian-grouped number without the ₹ symbol, e.g. "12,34,567". */
export function formatNumber(value: string | number | null | undefined): string {
  return groupFormatter.format(toNumber(value));
}

/** Parse a user-typed currency string ("12,34,567" / "₹1,200.50") to a number. */
export function parseInr(input: string): number {
  if (!input) return 0;
  const cleaned = input.replace(/[₹,\s]/g, '');
  const n = Number(cleaned);
  return Number.isFinite(n) ? n : 0;
}

const dateFormatter = new Intl.DateTimeFormat('en-IN', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
});

const dateTimeFormatter = new Intl.DateTimeFormat('en-IN', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

function toDate(value: string | number | Date | null | undefined): Date | null {
  if (value === null || value === undefined || value === '') return null;
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** "31 May 2026" (IST-rendered). Returns "—" for empty/invalid. */
export function formatDate(value: string | number | Date | null | undefined): string {
  const d = toDate(value);
  return d ? dateFormatter.format(d) : '—';
}

/** "31 May 2026, 06:30 PM". Returns "—" for empty/invalid. */
export function formatDateTime(value: string | number | Date | null | undefined): string {
  const d = toDate(value);
  return d ? dateTimeFormatter.format(d) : '—';
}

/** Coarse relative time, e.g. "2s ago", "5m ago", "3h ago", or a date. */
export function formatRelative(value: string | number | Date | null | undefined): string {
  const d = toDate(value);
  if (!d) return '—';
  const diffMs = Date.now() - d.getTime();
  const sec = Math.round(diffMs / 1000);
  if (sec < 60) return `${Math.max(sec, 0)}s ago`;
  const min = Math.round(sec / 60);
  if (min < 60) return `${min}m ago`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `${hr}h ago`;
  return formatDate(d);
}

/** Mask a PAN for display: "ABCDE1234F" -> "ABCDE****F". */
export function maskPan(pan: string | null | undefined): string {
  if (!pan) return '';
  const p = pan.trim().toUpperCase();
  if (p.length !== 10) return p;
  return `${p.slice(0, 5)}****${p.slice(9)}`;
}

/** Human file size, e.g. 1536 -> "1.5 KB". */
export function formatBytes(bytes: number | null | undefined): string {
  const b = toNumber(bytes);
  if (b <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  const i = Math.min(Math.floor(Math.log(b) / Math.log(1024)), units.length - 1);
  const v = b / Math.pow(1024, i);
  return `${v.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

/** Convert "AY2025-26" to a friendly "AY 2025-26". */
export function formatAssessmentYear(code: string | null | undefined): string {
  if (!code) return '';
  return code.replace(/^AY/i, 'AY ');
}
