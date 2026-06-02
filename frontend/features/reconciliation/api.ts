// features/reconciliation/api.ts — TanStack Query key + fetcher for a return's AIS/26AS reconciliation.
//   GET /returns/{id}/reconciliation  (read-only)

import { apiGet } from '@/lib/api';
import type { ReconciliationReportDto } from './types';

export const reconciliationKeys = {
  all: ['reconciliation'] as const,
  report: (returnId: string) => [...reconciliationKeys.all, returnId] as const,
};

export function getReconciliation(returnId: string): Promise<ReconciliationReportDto> {
  return apiGet<ReconciliationReportDto>(`/returns/${returnId}/reconciliation`);
}
