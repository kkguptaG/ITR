// ---------------------------------------------------------------------------
// features/accounting/types.ts
// TypeScript mirror of the backend Accounting DTOs (camelCase wire). Money is a
// JSON number (NUMERIC(14,2) → number); DateOnly serializes as "yyyy-MM-dd".
// Source of truth: backend Modules/Accounting/AccountingDtos.cs.
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime } from '@/lib/api-types';

export type LedgerGroup =
  | 'BankAccounts'
  | 'CashInHand'
  | 'SundryDebtors'
  | 'SundryCreditors'
  | 'SalesIncome'
  | 'OtherIncome'
  | 'PurchaseAccounts'
  | 'DirectExpenses'
  | 'IndirectExpenses'
  | 'DutiesAndTaxes'
  | 'LoansAndLiabilities'
  | 'FixedAssets'
  | 'Investments'
  | 'CapitalAccount'
  | 'Suspense';

export type LedgerNature = 'Asset' | 'Liability' | 'Income' | 'Expense' | 'Equity';

export type DrCr = 'Debit' | 'Credit';

export type VoucherType = 'Receipt' | 'Payment' | 'Contra' | 'Journal';

export type BankImportStatus =
  | 'Uploaded'
  | 'Parsing'
  | 'Parsed'
  | 'NeedsReview'
  | 'Posted'
  | 'Failed';

export type BankLineStatus = 'Suggested' | 'Confirmed' | 'Skipped' | 'Posted';

/** "yyyy-MM-dd" (DateOnly). */
export type DateString = string;

// ---- Chart of accounts ----------------------------------------------------

export interface LedgerDto {
  id: Guid;
  name: string;
  group: LedgerGroup;
  nature: LedgerNature;
  openingBalance: number;
  currentBalance: number;
  isBank: boolean;
  isSystemGenerated: boolean;
  notes: string | null;
  voucherCount: number;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

export interface CreateLedgerBody {
  name: string;
  group: LedgerGroup;
  openingBalance?: number;
  isBank?: boolean;
  notes?: string | null;
}

export interface UpdateLedgerBody {
  name: string;
  group: LedgerGroup;
  openingBalance?: number;
  notes?: string | null;
}

// ---- Bank statement import ------------------------------------------------

export interface BankImportDto {
  id: Guid;
  bankLedgerId: Guid;
  bankLedgerName: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: BankImportStatus;
  periodFrom: DateString | null;
  periodTo: DateString | null;
  lineCount: number;
  matchedCount: number;
  generatedLedgerCount: number;
  postedCount: number;
  warnings: string[];
  postedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

export interface BankLineDto {
  id: Guid;
  rowIndex: number;
  txnDate: DateString | null;
  narration: string;
  referenceNo: string | null;
  debit: number | null;
  credit: number | null;
  runningBalance: number | null;
  direction: DrCr;
  amount: number;
  suggestedLedgerId: Guid | null;
  suggestedLedgerName: string | null;
  suggestedGroup: LedgerGroup | null;
  suggestionIsNewLedger: boolean;
  matchConfidence: number;
  matchMethod: string | null;
  matchRationale: string | null;
  chosenLedgerId: Guid | null;
  status: BankLineStatus;
  voucherId: Guid | null;
}

export interface BankImportDetailDto {
  import: BankImportDto;
  lines: BankLineDto[];
}

// ---- Commit (post vouchers) ----------------------------------------------

export interface NewLedgerSpec {
  name: string;
  group: LedgerGroup;
}

export interface LineDecision {
  lineId: Guid;
  skip?: boolean;
  ledgerId?: Guid | null;
  newLedger?: NewLedgerSpec | null;
}

export interface PostImportBody {
  decisions?: LineDecision[];
  postUnlistedSuggestions?: boolean;
}

export interface PostImportResponse {
  import: BankImportDto;
  vouchersPosted: number;
  ledgersCreated: number;
  skipped: number;
  createdLedgers: LedgerDto[];
}
