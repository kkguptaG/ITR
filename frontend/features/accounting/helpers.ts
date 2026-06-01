// ---------------------------------------------------------------------------
// features/accounting/helpers.ts — presentational helpers (framework-free).
// Labels, badge tones and the " (E)" trace mark for the Accounting feature.
// ---------------------------------------------------------------------------

import type {
  BankImportStatus,
  BankLineStatus,
  LedgerGroup,
  LedgerNature,
} from './types';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

/** The suffix the backend appends to system-generated account heads. */
export const GENERATED_SUFFIX = '(E)';

/** True if a ledger name carries the system-generated trace mark. */
export function isGeneratedName(name: string | null | undefined): boolean {
  return !!name && name.trimEnd().endsWith(GENERATED_SUFFIX);
}

const GROUP_LABELS: Record<LedgerGroup, string> = {
  BankAccounts: 'Bank Accounts',
  CashInHand: 'Cash in Hand',
  SundryDebtors: 'Sundry Debtors',
  SundryCreditors: 'Sundry Creditors',
  SalesIncome: 'Sales / Income',
  OtherIncome: 'Other Income',
  PurchaseAccounts: 'Purchases',
  DirectExpenses: 'Direct Expenses',
  IndirectExpenses: 'Indirect Expenses',
  DutiesAndTaxes: 'Duties & Taxes',
  LoansAndLiabilities: 'Loans & Liabilities',
  FixedAssets: 'Fixed Assets',
  Investments: 'Investments',
  CapitalAccount: 'Capital Account',
  Suspense: 'Suspense',
};

export function formatGroup(group: LedgerGroup | string): string {
  return GROUP_LABELS[group as LedgerGroup] ?? group;
}

/** All groups as {label,value} for a <Select>, grouped by nature order. */
export const GROUP_OPTIONS: { value: LedgerGroup; label: string }[] = (
  Object.keys(GROUP_LABELS) as LedgerGroup[]
).map((g) => ({ value: g, label: GROUP_LABELS[g] }));

export function natureTone(nature: LedgerNature | string): Tone {
  switch (nature) {
    case 'Asset':
      return 'info';
    case 'Liability':
      return 'warning';
    case 'Income':
      return 'success';
    case 'Expense':
      return 'danger';
    case 'Equity':
      return 'brand';
    default:
      return 'neutral';
  }
}

const IMPORT_STATUS_LABELS: Record<BankImportStatus, string> = {
  Uploaded: 'Uploaded',
  Parsing: 'Parsing',
  Parsed: 'Parsed',
  NeedsReview: 'Needs review',
  Posted: 'Posted',
  Failed: 'Failed',
};

export function formatImportStatus(status: BankImportStatus | string): string {
  return IMPORT_STATUS_LABELS[status as BankImportStatus] ?? status;
}

const IMPORT_STATUS_TONES: Record<BankImportStatus, Tone> = {
  Uploaded: 'neutral',
  Parsing: 'info',
  Parsed: 'info',
  NeedsReview: 'warning',
  Posted: 'success',
  Failed: 'danger',
};

export function importStatusTone(status: BankImportStatus | string): Tone {
  return IMPORT_STATUS_TONES[status as BankImportStatus] ?? 'neutral';
}

const LINE_STATUS_TONES: Record<BankLineStatus, Tone> = {
  Suggested: 'info',
  Confirmed: 'brand',
  Skipped: 'neutral',
  Posted: 'success',
};

export function lineStatusTone(status: BankLineStatus | string): Tone {
  return LINE_STATUS_TONES[status as BankLineStatus] ?? 'neutral';
}

/** Confidence → tone: strong (≥80%), plausible (≥45%), weak otherwise. */
export function confidenceTone(confidence: number): Tone {
  if (confidence >= 0.8) return 'success';
  if (confidence >= 0.45) return 'warning';
  return 'danger';
}

export function formatConfidence(confidence: number): string {
  return `${Math.round((confidence ?? 0) * 100)}%`;
}

/** Whether a statement still has lines awaiting a posting decision. */
export function canReviewImport(status: BankImportStatus | string): boolean {
  return status === 'NeedsReview' || status === 'Parsed';
}
