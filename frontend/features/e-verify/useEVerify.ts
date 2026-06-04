// ---------------------------------------------------------------------------
// features/e-verify/useEVerify.ts
// Query + mutation hooks for post-filing e-verification. Verifying a return can
// flip its lifecycle (Filed -> Processed) and sets eVerifiedAt, so a successful
// confirm invalidates the filing detail/status queries too.
// ---------------------------------------------------------------------------

import { useCallback } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { filingKeys } from '@/features/filing/api';
import { confirmEVerify, eVerifyKeys, getEVerifyStatus, startEVerify } from './api';
import type { EVerifyMode } from './types';

export function useEVerifyStatus(returnId: string) {
  return useQuery({
    queryKey: eVerifyKeys.status(returnId),
    queryFn: () => getEVerifyStatus(returnId),
    enabled: !!returnId,
    staleTime: 10_000,
  });
}

export function useInvalidateEVerify(returnId: string) {
  const qc = useQueryClient();
  return useCallback(() => {
    void qc.invalidateQueries({ queryKey: eVerifyKeys.status(returnId) });
    // Verification can advance the return (Filed -> Processed) and stamps eVerifiedAt.
    void qc.invalidateQueries({ queryKey: filingKeys.detail(returnId) });
    void qc.invalidateQueries({ queryKey: filingKeys.status(returnId) });
  }, [qc, returnId]);
}

export function useStartEVerify(returnId: string) {
  return useMutation({
    mutationFn: (mode: EVerifyMode) => startEVerify(returnId, { mode }),
  });
}

export function useConfirmEVerify(returnId: string) {
  const invalidate = useInvalidateEVerify(returnId);
  return useMutation({
    mutationFn: (code?: string) => confirmEVerify(returnId, { code }),
    onSuccess: () => invalidate(),
  });
}
