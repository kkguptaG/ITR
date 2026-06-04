// ---------------------------------------------------------------------------
// features/e-verify/types.ts
// DTOs for post-filing e-verification. Mirrors the backend Modules/EVerify
// contracts exactly (string-enum members match the JsonStringEnumConverter).
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime } from '@/lib/api-types';

/** The six ways a filed return can be verified with the ITD. */
export type EVerifyMode =
  | 'AadhaarOtp'
  | 'NetBanking'
  | 'BankAccountEvc'
  | 'DematEvc'
  | 'BankAtmEvc'
  | 'ItrV';

/** Lifecycle of a single verification attempt. */
export type EVerifyStatus = 'Pending' | 'Verified' | 'Failed' | 'Expired';

/** GET /returns/{id}/e-verify, and the body of POST .../e-verify:confirm. */
export interface EVerificationStatusDto {
  returnId: Guid;
  isFiled: boolean;
  isVerified: boolean;
  mode: EVerifyMode | null;
  status: EVerifyStatus | null;
  transactionId: string | null;
  challengeExpiresAt: IsoDateTime | null;
  evcReference: string | null;
  filedAt: IsoDateTime | null;
  verifiedAt: IsoDateTime | null;
  /** Date (YYYY-MM-DD) by which the return must be verified — filing date + 30 days. */
  verifyBy: string | null;
  daysRemaining: number | null;
  isOverdue: boolean;
  availableModes: EVerifyMode[];
  failureReason: string | null;
}

/** Response of POST /returns/{id}/e-verify:start. */
export interface EVerificationStartResponse {
  returnId: Guid;
  mode: EVerifyMode;
  status: EVerifyStatus;
  transactionId: string | null;
  challengeExpiresAt: IsoDateTime | null;
  /** False for net-banking (pre-authenticated) and ITR-V (postal). */
  requiresCode: boolean;
  instruction: string;
  /** Populated only in the backend's Development environment — never in production. */
  devCode: string | null;
}

export interface EVerificationStartRequest {
  mode: EVerifyMode;
}

export interface EVerificationConfirmRequest {
  code?: string | null;
}
