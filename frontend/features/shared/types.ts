// ---------------------------------------------------------------------------
// features/shared/types.ts
// Plain, self-contained view-models for the cross-cutting presentational
// components in this folder (StatusTimeline / RefundTrackerCard /
// TaxSummaryPanel / RegimeCompareCard / DeductionSuggestionCard).
//
// These intentionally DO NOT import feature-local DTOs (e.g. from
// features/returns). Each component takes a small, explicit prop shape so any
// feature — the dashboard, the filing wizard, the CA workspace — can map its
// own DTOs onto these and reuse the UI without creating a folder dependency.
//
// Only shared enums come from lib/api-types so they never drift from the wire.
// ---------------------------------------------------------------------------

import type { DecimalString, IsoDateTime, Regime } from '@/lib/api-types';

/**
 * A single tax computation for one regime, reduced to the fields these cards
 * render. Money fields accept either the API's string-decimals or numbers — the
 * components coerce via lib/format.toNumber, so callers can pass either.
 *
 * This is a structural superset-compatible subset of both
 * lib/api-types.ComputationDto and features/returns TaxComputationDto, so an
 * object of either type can be passed directly.
 */
export interface ComputationView {
  regime: Regime;
  grossTotalIncome: DecimalString | number;
  totalDeductions: DecimalString | number;
  taxableIncome: DecimalString | number;
  taxBeforeCess: DecimalString | number;
  cess: DecimalString | number;
  rebate87A: DecimalString | number;
  surcharge: DecimalString | number;
  totalTax: DecimalString | number;
  tdsPaid: DecimalString | number;
  advanceTax: DecimalString | number;
  /** Positive = refund due, negative = tax payable. */
  refundOrPayable: DecimalString | number;
  /** Optional — present on persisted computations. */
  computedAt?: IsoDateTime;
}

/**
 * A deduction recommendation surfaced by the 80C/80D recommender (docs 03).
 * `headroom` is the additional amount the user could still claim under the
 * section's statutory cap; `potentialSaving` is the tax it would save.
 */
export interface DeductionSuggestionView {
  /** Chapter VI-A section, e.g. "80C", "80D", "80CCD(1B)". */
  section: string;
  /** Short human label, e.g. "Investments & insurance premiums". */
  title?: string;
  /** Amount already claimed/declared under this section. */
  claimed?: DecimalString | number;
  /** Statutory ceiling for the section (if capped). */
  cap?: DecimalString | number;
  /** Remaining room to invest/claim under the cap. */
  headroom: DecimalString | number;
  /** Estimated tax saved if the headroom is fully used (at the user's slab). */
  potentialSaving?: DecimalString | number;
  /** Optional explanatory note (e.g. "ELSS, PPF, life insurance, EPF"). */
  note?: string;
}
