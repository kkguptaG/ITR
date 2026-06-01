// ---------------------------------------------------------------------------
// features/returns/types.ts
// Wire types for the Returns/Filing module, mirroring the backend DTOs exactly
// (ASP.NET Core, camelCase wire). These are intentionally feature-local because
// the real backend contract (ReturnSummaryDto / ReturnDetailDto / ...) is richer
// and slightly different from the generic placeholders in lib/api-types.ts —
// e.g. itrType / regime are nullable on a draft, and the create body field is
// `assessmentYear` (not `assessmentYearCode`).
//
// Source of truth: backend Modules/Returns/ReturnDtos.cs + ItrSelectorDtos.cs.
// Shared enums are imported from lib/api-types so they never drift.
// ---------------------------------------------------------------------------

import type {
  DecimalString,
  Guid,
  IsoDateTime,
  ItrType,
  Regime,
  ReturnStatus,
} from '@/lib/api-types';

/** GET /returns row projection. itrType/regime are null until the user picks/auto-selects. */
export interface ReturnSummaryDto {
  id: Guid;
  assessmentYear: string; // "AY2025-26"
  itrType: ItrType | null;
  status: ReturnStatus;
  regime: Regime | null;
  acknowledgmentNumber: string | null;
  createdAt: IsoDateTime;
  submittedAt: IsoDateTime | null;
}

/** A single persisted (or freshly computed) tax computation for one regime. */
export interface TaxComputationDto {
  id: Guid;
  regime: Regime;
  grossTotalIncome: DecimalString;
  totalDeductions: DecimalString;
  taxableIncome: DecimalString;
  taxBeforeCess: DecimalString;
  cess: DecimalString;
  rebate87A: DecimalString;
  surcharge: DecimalString;
  totalTax: DecimalString;
  tdsPaid: DecimalString;
  advanceTax: DecimalString;
  interestPenalty: DecimalString;
  /** Positive = refund due, negative = payable. */
  refundOrPayable: DecimalString;
  isRecommended: boolean;
  computedAt: IsoDateTime;
}

/** GET /returns/{id} full detail (header + heads + latest computation). */
export interface ReturnDetailDto {
  id: Guid;
  assessmentYear: string;
  itrType: ItrType | null;
  status: ReturnStatus;
  regime: Regime | null;
  ruleSetVersion: string;
  questionnaireSchemaVersion: string;
  answersJson: string;
  filingMode: string;
  isRevised: boolean;
  acknowledgmentNumber: string | null;
  createdAt: IsoDateTime;
  submittedAt: IsoDateTime | null;
  eVerifiedAt: IsoDateTime | null;
  incomeSources: unknown[];
  salaries: unknown[];
  houseProperties: unknown[];
  capitalGains: unknown[];
  businessIncomes: unknown[];
  deductions: unknown[];
  latestComputation: TaxComputationDto | null;
  // Prepaid taxes (credits) + brought-forward losses captured on the return.
  tdsPaid: number;
  tcsPaid: number;
  advanceTaxPaid: number;
  selfAssessmentTaxPaid: number;
  broughtForwardHousePropertyLoss: number;
  broughtForwardBusinessLoss: number;
  broughtForwardShortTermCapitalLoss: number;
  broughtForwardLongTermCapitalLoss: number;
}


/** POST /returns body. itrType/regime optional — auto-selector classifies later. */
export interface CreateReturnBody {
  assessmentYear: string; // "AY2025-26"
  itrType?: ItrType;
  regime?: Regime;
}

/** GET /returns/{id}/status lifecycle projection. */
export interface ReturnStatusDto {
  id: Guid;
  status: ReturnStatus;
  acknowledgmentNumber: string | null;
  submittedAt: IsoDateTime | null;
  eVerifiedAt: IsoDateTime | null;
}

// ----------------------------------------------------------------- ITR selector
// GET /returns/selector?<flags> — stateless "which ITR form?" advisor.

export interface ItrSelectorInput {
  totalIncome?: number;
  hasSalaryOrPension?: boolean;
  housePropertyCount?: number;
  hasBroughtForwardLoss?: boolean;
  hasCapitalGains?: boolean;
  capitalGainsOnlyLtcg112A?: boolean;
  ltcg112AAmount?: number;
  hasBusinessIncome?: boolean;
  hasPresumptiveIncome?: boolean;
  hasSpeculativeIncome?: boolean;
  hasFnoIncome?: boolean;
  hasForeignAssetsOrIncome?: boolean;
  isDirectorOrUnlistedShares?: boolean;
  isNonResidentOrRnor?: boolean;
  hasAgriIncomeAbove5000?: boolean;
  isPartnerInFirm?: boolean;
  hasCryptoVda?: boolean;
}

export interface ItrSelectionVerdict {
  recommendedForm: ItrType;
  confidence: string;
  blockedForms: Record<string, string[]>;
  decidingFlags: string[];
  explanation: string;
}

// ----------------------------------------------------------------- /tax/slabs
// Used only to discover the active assessment-year code for the new-return dialog
// (there is no dedicated assessment-years endpoint). We read just `assessmentYear`.

export interface SlabsResponse {
  assessmentYear: string;
  ruleSetVersion: string;
}
