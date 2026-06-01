// ---------------------------------------------------------------------------
// features/returns/helpers.ts
// Small pure helpers shared by the dashboard + returns list (labels, routing,
// refund math). Keep UI-framework-free so they're trivially testable.
// ---------------------------------------------------------------------------

import type { ItrType, Regime, ReturnStatus } from '@/lib/api-types';
import { toNumber } from '@/lib/format';
import type { ReturnSummaryDto, TaxComputationDto } from './types';

/** "ITR1" -> "ITR-1". Null (not yet selected) renders as a friendly placeholder. */
export function formatItrType(itr: ItrType | null | undefined): string {
  if (!itr) return 'Not selected';
  return itr.replace(/^ITR/i, 'ITR-');
}

/** "Old" / "New" -> "Old regime" / "New regime". */
export function formatRegime(regime: Regime | null | undefined): string {
  if (!regime) return '—';
  return regime === 'Old' ? 'Old regime' : 'New regime';
}

/** Terminal states: the return is filed/processed and can only be viewed. */
const TERMINAL_STATUSES: ReadonlySet<ReturnStatus> = new Set<ReturnStatus>([
  'Filed',
  'Processed',
]);

/** A return is "continuable" (Continue CTA) until it's filed/processed. */
export function isContinuable(status: ReturnStatus): boolean {
  return !TERMINAL_STATUSES.has(status);
}

/** A return counts as "in progress" for KPI purposes if it isn't filed yet and isn't failed. */
export function isInProgress(status: ReturnStatus): boolean {
  return status !== 'Filed' && status !== 'Processed' && status !== 'Failed';
}

/**
 * The wizard entry route for a return. New/early drafts start at the personal
 * step; everything still-editable resumes at personal too (the wizard itself
 * decides the furthest reachable step). Filed/processed returns route to the
 * read-only detail view.
 */
export function returnHref(r: Pick<ReturnSummaryDto, 'id' | 'status'>): string {
  if (isContinuable(r.status)) {
    return `/returns/${r.id}/file/personal`;
  }
  return `/returns/${r.id}`;
}

/**
 * Signed refund/payable amount from a computation. The backend convention is
 * positive = refund due to the taxpayer, negative = payable. Returns 0 when no
 * computation exists yet.
 */
export function refundOrPayable(comp: TaxComputationDto | null | undefined): number {
  if (!comp) return 0;
  return toNumber(comp.refundOrPayable);
}
