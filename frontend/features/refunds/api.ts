// ---------------------------------------------------------------------------
// features/refunds/api.ts
// TanStack Query key + fetchers for post-processing refund tracking. Routes use
// the colon ":verb" sub-resource convention exactly as the backend declares them.
// ---------------------------------------------------------------------------

import { apiGet, apiPost } from '@/lib/api';
import type { RefundStatusDto } from './types';

export const refundKeys = {
  all: ['refund'] as const,
  status: (returnId: string) => [...refundKeys.all, 'status', returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/refund`;

export const getRefundStatus = (returnId: string) =>
  apiGet<RefundStatusDto>(base(returnId));

export const requestRefundReissue = (returnId: string) =>
  apiPost<RefundStatusDto>(`${base(returnId)}:reissue`, {});
