// ---------------------------------------------------------------------------
// features/taxes-paid/api.ts
// TanStack Query keys + fetchers for a return's TDS + self-paid challans.
//
// Backend routes (all owner-scoped, return-scoped):
//   GET    /returns/{id}/taxes-paid
//   POST   /returns/{id}/taxes-paid/tds      DELETE .../tds/{id}
//   POST   /returns/{id}/taxes-paid/challans DELETE .../challans/{id}
// ---------------------------------------------------------------------------

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type {
  ChallanDto,
  TaxesPaidSummaryDto,
  TdsEntryDto,
  UpsertChallanBody,
  UpsertTdsEntryBody,
} from './types';

export const taxesPaidKeys = {
  all: ['taxes-paid'] as const,
  summary: (returnId: string) => [...taxesPaidKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/taxes-paid`;

export function getTaxesPaid(returnId: string): Promise<TaxesPaidSummaryDto> {
  return apiGet<TaxesPaidSummaryDto>(base(returnId));
}

export function addTds(returnId: string, body: UpsertTdsEntryBody): Promise<TdsEntryDto> {
  return apiPost<TdsEntryDto>(`${base(returnId)}/tds`, body);
}

export function deleteTds(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/tds/${id}`);
}

export function addChallan(returnId: string, body: UpsertChallanBody): Promise<ChallanDto> {
  return apiPost<ChallanDto>(`${base(returnId)}/challans`, body);
}

export function deleteChallan(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/challans/${id}`);
}
