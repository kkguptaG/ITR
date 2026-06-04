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
  TcsEntryDto,
  TdsCodeRecord,
  TdsEntryDto,
  UpsertChallanBody,
  UpsertTcsEntryBody,
  UpsertTdsEntryBody,
} from './types';

export const taxesPaidKeys = {
  all: ['taxes-paid'] as const,
  summary: (returnId: string) => [...taxesPaidKeys.all, returnId] as const,
  tdsCodes: ['reference', 'tds-codes'] as const,
};

/** The ITD TDS section/deductee codes (reference master) for the section picker. */
export function getTdsCodes(): Promise<TdsCodeRecord[]> {
  return apiGet<TdsCodeRecord[]>('/reference/tds-codes');
}

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

export function addTcs(returnId: string, body: UpsertTcsEntryBody): Promise<TcsEntryDto> {
  return apiPost<TcsEntryDto>(`${base(returnId)}/tcs`, body);
}

export function deleteTcs(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/tcs/${id}`);
}
