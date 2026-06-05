// ---------------------------------------------------------------------------
// features/filing/types.ts
// Wire types for the FILING WIZARD, mirroring the ACTUAL backend DTOs exactly
// (ASP.NET Core, camelCase wire). These are intentionally feature-local because
// the live contract in the controllers is richer/different from the generic
// placeholders in lib/api-types.ts. Sources of truth (read, not guessed):
//   • Api/Modules/Returns/ReturnDtos.cs     (header, income heads, deductions, submit)
//   • Api/Modules/Tax/TaxDtos.cs            (compute / regime-compare / recommendations / slabs)
//   • Api/Modules/Documents/DocumentDtos.cs (two-step upload, extraction, approve)
//   • Api/Modules/Payments/PaymentDtos.cs   (plans, orders, verify, coupons)
//   • Api/Modules/Ca/CaDtos.cs              (assignment)
// Shared enums come from lib/api-types so they never drift.
// ---------------------------------------------------------------------------

import type {
  DecimalString,
  Guid,
  IsoDateTime,
  ItrType,
  Regime,
  ReturnStatus,
} from '@/lib/api-types';

// Re-export the feature-local return types the wizard reuses from the returns module.
export type {
  ReturnDetailDto,
  ReturnSummaryDto,
  ReturnStatusDto,
  TaxComputationDto,
  ItrSelectorInput,
  ItrSelectionVerdict,
  SlabsResponse,
} from '@/features/returns/types';

// Money is decimal in C#; ASP.NET serializes it as a JSON number. We accept both
// number and string defensively and normalise with lib/format.toNumber.
export type Money = number | DecimalString;

// Backend enum names that aren't in lib/api-types (richer than the generic set).
export type HousePropertyType = 'SelfOccupied' | 'LetOut' | 'DeemedLetOut';
export type CapitalGainAssetType =
  | 'ListedEquity'
  | 'EquityMutualFund'
  | 'DebtMutualFund'
  | 'UnlistedShares'
  | 'ImmovableProperty'
  | 'AgriculturalLand'
  | 'Bonds'
  | 'Gold'
  | 'Jewellery'
  | 'CryptoVda'
  | 'Other';
export type CapitalGainTerm = 'Short' | 'Long';
export type CapitalGainAcquisitionMode = 'Purchase' | 'Gift' | 'Inheritance' | 'Will' | 'Other';
export type CapitalGainSubType =
  | 'ListedShare' | 'UnlistedShare' | 'IpoShare' | 'EsopShare' | 'RsuShare' | 'BonusShare' | 'RightsShare' | 'Buyback'
  | 'EquityMutualFund' | 'DebtMutualFund' | 'HybridMutualFund' | 'InternationalFund'
  | 'ResidentialHouse' | 'CommercialProperty' | 'Plot' | 'AgriculturalLand'
  | 'PhysicalGold' | 'GoldEtf' | 'SovereignGoldBond' | 'Jewellery' | 'OtherBullion'
  | 'ListedBond' | 'Debenture' | 'GovernmentSecurity' | 'TaxFreeBond'
  | 'Crypto' | 'Nft'
  | 'ForeignShare' | 'UsStock' | 'ForeignEtf' | 'ForeignRsu' | 'AdrGdr'
  | 'Goodwill' | 'IntangibleAsset' | 'ArtCollectible' | 'Vehicle' | 'IpRights' | 'SlumpSale' | 'Other';
export type IncomeType =
  | 'Salary'
  | 'HouseProperty'
  | 'CapitalGains'
  | 'Business'
  | 'OtherSources';
export type SalaryComponentCategory = 'Salary' | 'Perquisite' | 'ProfitInLieu' | 'Allowance';

// ------------------------------------------------------------ income heads (Returns module)

export interface IncomeSourceDto {
  id: Guid;
  type: IncomeType;
  label: string | null;
  amount: number;
  sourceMetaJson: string;
}
export interface UpsertIncomeSourceRequest {
  type: IncomeType;
  label?: string | null;
  amount: number;
  sourceMetaJson?: string | null;
}

export interface SalaryComponentDto {
  id: Guid;
  label: string;
  category: SalaryComponentCategory;
  total: number;
  exempt: number;
  taxable: number;
  isHra: boolean;
}
export interface UpsertSalaryComponentRequest {
  label: string;
  category: SalaryComponentCategory;
  total: number;
  exempt: number;
  isHra?: boolean;
}
export interface SalaryDetailDto {
  id: Guid;
  employer: string;
  tan: string | null;
  gross: number;
  hra: number;
  perquisites: number;
  profitsInLieu: number;
  exemptAllowances: number;
  hraExemption: number;
  stdDeduction: number;
  professionalTax: number;
  components: SalaryComponentDto[];
}
export interface UpsertSalaryRequest {
  employer: string;
  tan?: string | null;
  gross: number;
  hra: number;
  perquisites: number;
  profitsInLieu: number;
  exemptAllowances: number;
  hraExemption: number;
  stdDeduction: number;
  professionalTax: number;
  components?: UpsertSalaryComponentRequest[];
}

export interface HousePropertyDto {
  id: Guid;
  type: HousePropertyType;
  address: string | null;
  annualValue: number;
  annualRent: number;
  municipalTaxPaid: number;
  stdDeduction30Pct: number;
  interestOnLoan: number;
  coOwnerSharePct: number;
  netIncome: number;
}
export interface UpsertHousePropertyRequest {
  type: HousePropertyType;
  address?: string | null;
  annualValue: number;
  annualRent: number;
  municipalTaxPaid: number;
  interestOnLoan: number;
  coOwnerSharePct: number;
}

export interface CapitalGainLot {
  acquisitionDate: string | null;
  quantity: number;
  cost: number;
  fairMarketValue31Jan2018: number;
}

/** One row of the multi-section "Exempt Capital Gain" chart. */
export interface CapitalGainExemption {
  section: string;
  costOfNewAsset: number;
  cgasDeposit: number;
  dateOfAcquisition: string | null;
}

/** One row of the "Deemed Capital Gain" chart (clawback of a prior-year exemption). */
export interface CapitalGainDeemed {
  section: string;
  costOfNewAsset: number;
  cgasDeposit: number;
  dateOfAcquisition: string | null;
  deemedIncome: number;
}

export interface CapitalGainDto {
  id: Guid;
  assetType: CapitalGainAssetType;
  term: CapitalGainTerm;
  taxSection: string | null;
  acquisitionDate: string | null;
  transferDate: string | null;
  salePrice: number;
  costOfAcquisition: number;
  indexedCost: number;
  costOfImprovement: number;
  improvementDate: string | null;
  expensesOnTransfer: number;
  exemptionSection: string | null;
  exemptionAmount: number;
  reinvestmentAmount: number;
  gain: number;
  isin: string | null;
  fairMarketValue31Jan2018: number;
  acquisitionMode: CapitalGainAcquisitionMode;
  previousOwnerAcquisitionDate: string | null;
  previousOwnerCost: number;
  isRuralAgriculturalLand: boolean;
  exemptUnderDtaa: boolean;
  subType: CapitalGainSubType | null;
  sttPaid: boolean;
  tdsOnSale: number;
  tdsSection: string | null;
  coOwnerPercent: number;
  lots: CapitalGainLot[];
  exemptions: CapitalGainExemption[];
  deemedGains: CapitalGainDeemed[];
}
export interface UpsertCapitalGainRequest {
  assetType: CapitalGainAssetType;
  term: CapitalGainTerm;
  taxSection?: string | null;
  acquisitionMode?: CapitalGainAcquisitionMode;
  acquisitionDate?: string | null;
  transferDate?: string | null;
  previousOwnerAcquisitionDate?: string | null;
  previousOwnerCost?: number;
  isRuralAgriculturalLand?: boolean;
  exemptUnderDtaa?: boolean;
  salePrice: number;
  costOfAcquisition: number;
  costOfImprovement: number;
  improvementDate?: string | null;
  expensesOnTransfer: number;
  exemptionSection?: string | null;
  exemptionAmount: number;
  reinvestmentAmount?: number;
  isin?: string | null;
  fairMarketValue31Jan2018?: number;
  subType?: CapitalGainSubType | null;
  sttPaid?: boolean;
  tdsOnSale?: number;
  tdsSection?: string | null;
  coOwnerPercent?: number;
  lots?: CapitalGainLot[];
  exemptions?: CapitalGainExemption[];
  deemedGains?: CapitalGainDeemed[];
}

// --- Bulk import (P4) ---
export interface CapitalGainImportRequest {
  profileId: string;
  csv: string;
  commit?: boolean;
}
export interface ParseCapitalGainDocumentRequest {
  documentId: string;
  commit?: boolean;
}
export interface ImportedCgRow {
  row: number;
  assetType: CapitalGainAssetType;
  term: CapitalGainTerm;
  acquisitionDate: string | null;
  transferDate: string | null;
  salePrice: number;
  costOfAcquisition: number;
  expensesOnTransfer: number;
  isin: string | null;
  duplicate: boolean;
  errors: string[];
  ok: boolean;
}
export interface CapitalGainImportResult {
  profileId: string;
  totalRows: number;
  validRows: number;
  duplicateRows: number;
  errorRows: number;
  importedRows: number;
  rows: ImportedCgRow[];
}

// --- Intelligence layer (P6) ---
export type CgComplianceLevel = 'Green' | 'Yellow' | 'Red';
export type CgInsightSeverity = 'Info' | 'Tip' | 'Warning' | 'Risk';
export interface CgInsight {
  code: string;
  severity: CgInsightSeverity;
  count: number;
}
export interface CgInsightsResult {
  compliance: CgComplianceLevel;
  score: number;
  insights: CgInsight[];
}

export interface BusinessFinancialParticulars {
  partnerCapital: number;
  securedLoans: number;
  unsecuredLoans: number;
  sundryCreditors: number;
  fixedAssets: number;
  inventory: number;
  sundryDebtors: number;
  bankBalance: number;
  cashBalance: number;
}

export interface BusinessIncomeDto extends BusinessFinancialParticulars {
  id: Guid;
  natureOfBusinessCode: string | null;
  accountingMethod: string;
  isPresumptive: boolean;
  presumptiveSection: string | null;
  turnover: number;
  grossReceiptsDigital: number;
  grossReceiptsCash: number;
  presumptiveRatePct: number;
  netProfit: number;
  speculativeFlag: boolean;
  gstTurnoverReported: number;
  /** JSON array of 44AE goods-carriage vehicles. */
  goodsCarriageJson: string;
}
export interface UpsertBusinessIncomeRequest extends Partial<BusinessFinancialParticulars> {
  natureOfBusinessCode?: string | null;
  accountingMethod?: string | null;
  isPresumptive: boolean;
  presumptiveSection?: string | null;
  turnover: number;
  grossReceiptsDigital: number;
  grossReceiptsCash: number;
  netProfit: number;
  speculativeFlag: boolean;
  gstTurnoverReported: number;
  goodsCarriageJson?: string | null;
}

export interface DeductionDto {
  id: Guid;
  section: string;
  subType: string | null;
  description: string | null;
  amount: number;
  eligibleAmount: number | null;
  regimeApplicable: Regime | null;
}
export interface UpsertDeductionRequest {
  section: string;
  subType?: string | null;
  description?: string | null;
  amount: number;
  regimeApplicable?: Regime | null;
}

// ------------------------------------------------------------ validate / submit (Returns)

export interface ValidationFinding {
  severity: string; // "block" | "warn" | "info"
  code: string;
  message: string;
  field: string | null;
}
export interface ValidateReturnResponse {
  canFile: boolean;
  findings: ValidationFinding[];
}
export interface SubmitReturnResponse {
  id: Guid;
  status: ReturnStatus;
  acknowledgmentNumber: string;
  submittedAt: IsoDateTime;
  versionNo: number;
  snapshotHash: string;
}

// ------------------------------------------------------------ tax compute (Tax module)

export interface TraceLineDto {
  step: string;
  description: string;
  amount: number;
  ruleRef: string | null;
}
export interface TaxComputationResultDto {
  regime: Regime;
  grossTotalIncome: number;
  totalDeductions: number;
  taxableIncome: number;
  taxBeforeRebate: number;
  rebate87A: number;
  surcharge: number;
  cess: number;
  totalTax: number;
  /** TDS + TCS combined (used for refund/payable math). */
  tdsPaid: number;
  /** TCS component of tdsPaid — shown separately in the summary. */
  tcsPaid: number;
  /** Advance tax + self-assessment tax combined. */
  advanceTax: number;
  /** Self-assessment-tax component of advanceTax — shown separately. */
  selfAssessmentTaxPaid: number;
  interestPenalty: number;
  interest234A: number;
  interest234B: number;
  interest234C: number;
  /** s.234F late-filing fee (flat) — folded into the refund/payable. 0 for an on-time return. */
  lateFilingFee234F: number;
  /** Positive = refund due, negative = payable. */
  refundOrPayable: number;
  // AMT (s.115JC/JD) + reliefs (s.89/90/91); 0 when not applicable.
  adjustedTotalIncome: number;
  alternativeMinimumTax: number;
  amtCreditGenerated: number;
  amtCreditSetOff: number;
  relief89: number;
  relief90And91: number;
  // Current-year losses carried forward after inter-head set-off (s.71); 0 when none.
  housePropertyLossCarriedForward: number;
  businessLossCarriedForward: number;
  speculativeLossCarriedForward: number;
  shortTermCapitalLossCarriedForward: number;
  longTermCapitalLossCarriedForward: number;
  unabsorbedDepreciationCarriedForward: number;
  // Per-head net income as it flows into GTI — drives the computation dashboard's clickable head lines.
  salaryNetIncome: number;
  housePropertyNetIncome: number;
  businessNetIncome: number;
  capitalGainsNetIncome: number;
  otherSourcesNetIncome: number;
  /** Rate-wise split of income taxed outside the slab (Schedule SI) — drives the dashboard's capital-gains sub-lines. */
  specialIncome: SpecialIncomeDto;
  /** Tax on normal (slab-rate) income, before rebate. */
  taxAtNormalRates: number;
  /** Tax on income charged at special rates (CG 111A/112A/112/115BBH + casual 115BB). */
  taxAtSpecialRates: number;
  /** Net agricultural income — exempt, aggregated for the rate. */
  netAgriculturalIncome: number;
  trace: TraceLineDto[];
}

/** Rate-wise breakdown of income taxed outside the normal slab (ITD Schedule SI). */
export interface SpecialIncomeDto {
  /** STCG taxed at the normal slab rate (debt MF, unlisted, property STCG). */
  slabRateCapitalGains: number;
  /** STCG on listed equity (s.111A) @ 15% / 20%. */
  stcg111A: number;
  /** Taxable LTCG on listed equity (s.112A) @ 10% / 12.5%. */
  ltcg112A: number;
  /** LTCG on property / other (s.112) @ 20% / 12.5%. */
  ltcg112: number;
  /** Virtual digital asset gains (s.115BBH) @ 30%. */
  vda115BBH: number;
  /** Winnings / casual income (s.115BB) @ 30%. */
  casual115BB: number;
}
/** POST /tax/compute and /tax/regime-compare response (both regimes + recommendation). */
export interface ComputeResponse {
  returnId: Guid;
  assessmentYear: string;
  ruleSetVersion: string;
  recommendedRegime: Regime;
  savingsVsAlternative: number;
  reason: string;
  old: TaxComputationResultDto;
  new: TaxComputationResultDto;
}
export interface ComputeRequest {
  returnId: Guid;
  regime?: Regime | null;
}

// ------------------------------------------------------------ recommendations (Tax module)

export interface DeductionSuggestionDto {
  rank: number;
  section: string;
  label: string;
  gapToInvest: number;
  marginalTaxSaved: number;
  roiPerRupee: number;
  lockInYears: number;
  liquidity: number;
  utilityNote: string;
}
export interface RecommendationsResponse {
  oldRegimeTax: number;
  newRegimeTax: number;
  regimeSwitchBeatsDeductions: boolean;
  headline: string;
  suggestions: DeductionSuggestionDto[];
}
export interface RecommendationsRequest {
  returnId?: Guid | null;
}

// ------------------------------------------------------------ documents (Documents module)

export interface InitiateUploadRequest {
  kind: string; // DocumentKind name
  fileName: string;
  contentType: string;
  returnId?: Guid | null;
}
export interface InitiateUploadResponse {
  documentId: Guid;
  uploadUrl: string;
  uploadMethod: string; // "PUT"
  uploadHeaders: Record<string, string>;
  expiresAt: IsoDateTime;
}
export interface CompleteUploadRequest {
  eTag?: string | null;
  sha256?: string | null;
}
export interface DocumentDto {
  id: Guid;
  returnId: Guid | null;
  kind: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: string; // DocumentStatus name
  sha256: string | null;
  hasExtraction: boolean;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}
export interface ExtractedFieldDto {
  key: string;
  value: string | null;
  confidence: number | null; // 0..1
}
export interface ExtractionDto {
  id: Guid;
  documentId: Guid;
  docClass: string;
  status: string;
  confidenceScore: number | null;
  fieldsJson: string;
  fields: ExtractedFieldDto[];
  needsReview: boolean;
  reviewedByUserId: Guid | null;
  reviewedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}
export interface ApproveExtractionRequest {
  mapToReturn?: boolean;
  fieldOverrides?: Record<string, string> | null;
}
export interface ApproveExtractionResponse {
  extraction: ExtractionDto;
  incomeSourcesUpserted: number;
  deductionsUpserted: number;
}

// ------------------------------------------------------------ payments (Payments module)

export interface PlanDto {
  id: Guid;
  code: string;
  name: string;
  price: number;
  billingPeriod: string;
  features: string[];
  isActive: boolean;
}
export interface CreateOrderRequest {
  returnId: Guid;
  planCode: string;
  couponCode?: string | null;
  gateway?: string | null; // "razorpay" | "cashfree" | "wallet"
  useWallet?: boolean;
}
export interface CreateOrderResponse {
  paymentId: Guid;
  gateway: string;
  gatewayOrderId: string | null;
  gatewayKeyId: string | null;
  currency: string;
  baseAmount: number;
  discountAmount: number;
  walletApplied: number;
  gstAmount: number;
  amountPayable: number;
  status: string;
  requiresGatewayCheckout: boolean;
}
export interface VerifyPaymentRequest {
  gatewayPaymentId: string;
  signature: string;
}
export interface VerifyPaymentResponse {
  paymentId: Guid;
  status: string;
  invoiceId: Guid | null;
  invoiceNumber: string | null;
  taxReturnId: Guid | null;
  returnStatus: string | null;
}
export interface CouponValidateRequest {
  code: string;
  planCode: string;
}
export interface CouponResultDto {
  code: string;
  valid: boolean;
  type: string;
  value: number;
  baseAmount: number;
  discountAmount: number;
  netAmount: number;
  message: string | null;
}

// ------------------------------------------------------------ CA workflow (Ca module)

export interface AssignmentDto {
  assignmentId: Guid;
  taxReturnId: Guid;
  caUserId: Guid;
  status: string;
  priority: number;
  slaDueAt: IsoDateTime | null;
  assignedAt: IsoDateTime;
  completedAt: IsoDateTime | null;
  returnStatus: ReturnStatus;
}

// Convenience: the filing mode the user chooses on the final step.
export type FilingMode = 'self' | 'ca';

/** One NSE symbol's 31-Jan-2018 FMV (s.112A grandfathering), from the bundled master. */
export interface GrandfatherFmvRecord {
  symbol: string;
  fmv: number;
}
export type { ItrType, Regime, ReturnStatus, Guid };
