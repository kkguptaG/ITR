// ---------------------------------------------------------------------------
// features/ca/helpers.ts
// Small pure helpers for the CA workflow (labels, badge tones, SLA urgency,
// refund/payable sign). Framework-free so they're trivially testable.
// ---------------------------------------------------------------------------

import type { AssignmentStatus } from '@/lib/api-types';
import { toNumber } from '@/lib/format';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

/** AssignmentStatus → badge tone (consistent across queue + detail). */
export const assignmentStatusTone: Record<AssignmentStatus, Tone> = {
  Unassigned: 'warning',
  Assigned: 'info',
  InReview: 'brand',
  Completed: 'success',
};

/** A signed refund/payable (positive = refund, negative = payable). */
export function refundOrPayableNumber(value: string | number | null | undefined): number {
  return toNumber(value);
}

/**
 * SLA urgency bucket relative to now. `null` when there is no SLA. Used to tint
 * the due-date chip: overdue (red) · soon ≤24h (amber) · ok (neutral).
 */
export type SlaUrgency = 'overdue' | 'soon' | 'ok';

export function slaUrgency(slaDueAt: string | null | undefined, now: number = Date.now()): SlaUrgency | null {
  if (!slaDueAt) return null;
  const due = new Date(slaDueAt).getTime();
  if (Number.isNaN(due)) return null;
  if (due <= now) return 'overdue';
  if (due - now <= 24 * 60 * 60 * 1000) return 'soon';
  return 'ok';
}

export const slaTone: Record<SlaUrgency, Tone> = {
  overdue: 'danger',
  soon: 'warning',
  ok: 'neutral',
};

/** Priority (lower number = more urgent on the backend) → short label. */
export function priorityLabel(priority: number): string {
  if (priority <= 1) return 'High';
  if (priority === 2) return 'Normal';
  return 'Low';
}

export const priorityTone: Record<string, Tone> = {
  High: 'danger',
  Normal: 'info',
  Low: 'neutral',
};
