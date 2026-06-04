// ---------------------------------------------------------------------------
// features/refunds/useRefund.ts
// Query + mutation hooks for post-processing refund tracking. Refund status moves
// over days at the ITD, so we fetch on mount (and on window focus) and offer a
// manual "check for updates" refetch rather than hammering the feed on a timer.
// ---------------------------------------------------------------------------

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getRefundStatus, refundKeys, requestRefundReissue } from './api';

const SETTLED = [
  'RefundPaid',
  'NoRefundOrDemand',
  'DemandDetermined',
  'RefundAdjusted',
  'RefundFailed',
] as const;

/** True once the ITD has nothing more to report (terminal state). */
export function isRefundSettled(status: string | null | undefined): boolean {
  return !!status && (SETTLED as readonly string[]).includes(status);
}

export function useRefundStatus(returnId: string) {
  return useQuery({
    queryKey: refundKeys.status(returnId),
    queryFn: () => getRefundStatus(returnId),
    enabled: !!returnId,
    staleTime: 30_000,
  });
}

export function useRequestReissue(returnId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => requestRefundReissue(returnId),
    onSuccess: (data) => qc.setQueryData(refundKeys.status(returnId), data),
  });
}
