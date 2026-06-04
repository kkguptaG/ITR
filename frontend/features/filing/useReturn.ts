'use client';

// ---------------------------------------------------------------------------
// features/filing/useReturn.ts
// Shared TanStack Query hooks for the wizard's return context. The detail query
// is the spine: every step reads header/heads/computation from it, and mutations
// invalidate it so the Stepper + summary stay consistent.
// ---------------------------------------------------------------------------

import { useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { ReturnStatus } from '@/lib/api-types';
import { returnsKeys } from '@/features/returns/api';
import { filingKeys, getReturn } from './api';
import type { ReturnDetailDto } from './types';
import { WIZARD_STEPS, type WizardStepSlug } from './steps';

/** Load (and cache) the full return detail. Disabled until an id is known. */
export function useReturnDetail(returnId: string) {
  return useQuery({
    queryKey: filingKeys.detail(returnId),
    queryFn: () => getReturn(returnId),
    enabled: !!returnId,
    staleTime: 5_000,
  });
}

/** Invalidate the return detail + any list caches after a mutation. */
export function useInvalidateReturn(returnId: string) {
  const qc = useQueryClient();
  return useCallback(() => {
    void qc.invalidateQueries({ queryKey: filingKeys.detail(returnId) });
    void qc.invalidateQueries({ queryKey: filingKeys.status(returnId) });
    void qc.invalidateQueries({ queryKey: returnsKeys.lists() });
  }, [qc, returnId]);
}

/**
 * The furthest step the user may jump to via the Stepper, derived from status.
 * Filing is gated (you can't reach Summary before income exists, etc.) — but for
 * a forgiving demo UX we unlock steps based on lifecycle position rather than
 * hard data presence, and let each step validate on submit.
 */
export function furthestStep(detail: ReturnDetailDto | undefined): WizardStepSlug {
  if (!detail) return 'personal';
  const status: ReturnStatus = detail.status;
  switch (status) {
    case 'Filed':
    case 'Processed':
      return 'file';
    case 'Paid':
    case 'ReadyToFile':
    case 'UnderCaReview':
      return 'file';
    case 'PendingPayment':
      return 'payment';
    case 'ComputedReady':
      // Computation is done and reviewed on Summary — the user must be able to
      // advance to Payment, where creating the order transitions the return to
      // PendingPayment. Capping the furthest step at 'summary' deadlocked the
      // wizard (Payment was unreachable, so the order that moves the status
      // forward could never be created).
      return 'payment';
    case 'InProgress':
      return 'regime';
    case 'Draft':
    default:
      return detail.itrType ? 'income' : 'personal';
  }
}

export function isStepReachable(
  slug: WizardStepSlug,
  detail: ReturnDetailDto | undefined,
): boolean {
  const maxIdx = WIZARD_STEPS.indexOf(furthestStep(detail));
  return WIZARD_STEPS.indexOf(slug) <= maxIdx;
}

/** A return whose lifecycle has passed editing is read-only in the wizard. */
export function isReturnLocked(detail: ReturnDetailDto | undefined): boolean {
  return detail?.status === 'Filed' || detail?.status === 'Processed';
}
