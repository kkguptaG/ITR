// ---------------------------------------------------------------------------
// features/refunds/types.ts
// DTOs for post-processing income-tax refund/demand tracking. Mirrors the backend
// Modules/Refunds contract (string-enum members match the JsonStringEnumConverter).
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime } from '@/lib/api-types';

export type RefundStatus =
  | 'NotDetermined'
  | 'NoRefundOrDemand'
  | 'RefundDetermined'
  | 'RefundSentToBank'
  | 'RefundPaid'
  | 'RefundFailed'
  | 'RefundAdjusted'
  | 'DemandDetermined';

/** GET /returns/{id}/refund, and the body of POST .../refund:reissue. */
export interface RefundStatusDto {
  returnId: Guid;
  isProcessed: boolean;
  status: RefundStatus;
  determinedAmount: number;
  demandAmount: number;
  mode: string | null;
  refundSequenceNo: string | null;
  creditedAccountLast4: string | null;
  refundBankName: string | null;
  intimationDate: IsoDateTime | null;
  paidAt: IsoDateTime | null;
  failureReason: string | null;
  reissueCount: number;
  canReissue: boolean;
}
