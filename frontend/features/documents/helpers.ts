// ---------------------------------------------------------------------------
// features/documents/helpers.ts
// Pure presentational helpers for the Documents area: human labels for the
// DocumentKind / DocumentStatus enums, the badge tone mapping, and a small
// "is this a money field?" heuristic used to colour low-confidence values in
// the extraction review drawer. UI-framework-free so they stay testable.
// ---------------------------------------------------------------------------

import type { DocumentKind, DocumentStatus } from './types';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

/** Friendly label for each upload kind (shown in the picker + the table). */
const KIND_LABELS: Record<DocumentKind, string> = {
  Form16: 'Form 16',
  Form16A: 'Form 16A',
  Form26AS: 'Form 26AS',
  AIS: 'AIS',
  TIS: 'TIS',
  BankStatement: 'Bank statement',
  CapitalGainStmt: 'Capital gains statement',
  SalarySlip: 'Salary slip',
  GstData: 'GST data',
  RentReceipt: 'Rent receipt',
  InvestmentProof: 'Investment proof (80C/80D)',
  Other: 'Other',
};

export function formatDocumentKind(kind: DocumentKind | string): string {
  return KIND_LABELS[kind as DocumentKind] ?? kind;
}

/** The kinds offered in the upload picker, in a sensible filing order. */
export const UPLOAD_KIND_OPTIONS: { value: DocumentKind; label: string }[] = (
  [
    'Form16',
    'Form16A',
    'Form26AS',
    'AIS',
    'TIS',
    'SalarySlip',
    'BankStatement',
    'CapitalGainStmt',
    'InvestmentProof',
    'RentReceipt',
    'GstData',
    'Other',
  ] as DocumentKind[]
).map((value) => ({ value, label: KIND_LABELS[value] }));

/** Human label for a document status. */
const STATUS_LABELS: Record<DocumentStatus, string> = {
  Uploaded: 'Uploaded',
  Scanning: 'Scanning',
  Extracting: 'Extracting',
  Extracted: 'Extracted',
  NeedsReview: 'Needs review',
  Verified: 'Verified',
  Failed: 'Failed',
};

export function formatDocumentStatus(status: DocumentStatus | string): string {
  return STATUS_LABELS[status as DocumentStatus] ?? status;
}

/** Badge tone for each document status (consistent across the app). */
const STATUS_TONES: Record<DocumentStatus, Tone> = {
  Uploaded: 'info',
  Scanning: 'neutral',
  Extracting: 'info',
  Extracted: 'brand',
  NeedsReview: 'warning',
  Verified: 'success',
  Failed: 'danger',
};

export function documentStatusTone(status: DocumentStatus | string): Tone {
  return STATUS_TONES[status as DocumentStatus] ?? 'neutral';
}

/** Statuses where the user can act on an extraction (review / approve). */
export function canReviewExtraction(doc: { status: DocumentStatus; hasExtraction: boolean }): boolean {
  return doc.hasExtraction && (doc.status === 'Extracted' || doc.status === 'NeedsReview');
}

/** Money-field confidence gate (Ch.5 §5.2.4) — below this, a field needs review. */
export const CONFIDENCE_THRESHOLD = 0.92;

/**
 * Heuristic: does this extraction key carry a money amount? Used only for
 * display (the server is the authority on gating). Matches the mock extractor's
 * canonical keys: grossSalary, tdsDeducted, sec80c, interestIncome, ltcg, etc.
 */
export function isMoneyField(key: string): boolean {
  return /(salary|amount|income|tds|tax|deduction|interest|gain|turnover|value|sec80|hra|rent|premium|principal|paid|credit|balance)/i.test(
    key,
  );
}

/** "grossSalary" / "sec_80c" → "Gross salary" / "Sec 80c" for the review UI. */
export function humanizeFieldKey(key: string): string {
  const spaced = key
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z\d])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
