// ---------------------------------------------------------------------------
// features/bank-accounts/types.ts
// DTOs mirroring the backend BankAccounts module (camelCase on the wire).
// ---------------------------------------------------------------------------

/** A saved bank account. The account number is masked (last 4 only) — PII. */
export interface BankAccountDto {
  id: string;
  bankName: string;
  accountNumberMasked: string;
  accountType: string;
  ifsc: string;
  useForRefund: boolean;
}

/** POST body to add a bank account. All four fields are mandatory (ITR BankDetailType). */
export interface UpsertBankAccountBody {
  bankName: string;
  accountNumber: string;
  accountType: string;
  ifsc: string;
  useForRefund?: boolean;
}

/** GET /ifsc/{code} — bank + branch resolved from the bundled RBI master. */
export interface IfscRecord {
  ifsc: string;
  bank: string;
  branch: string;
}

/** The ITR AccountType enum, in display order. */
export const ACCOUNT_TYPES = ['SB', 'CA', 'CC', 'OD', 'NRO', 'OTH'] as const;
export type AccountType = (typeof ACCOUNT_TYPES)[number];
