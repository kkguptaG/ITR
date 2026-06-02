// features/pass-through-income/api.ts — Schedule PTI pass-through income (list / add / delete).
//   GET/POST /returns/{id}/pass-through-income   DELETE .../{id}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { PassThroughIncomeDto, UpsertPassThroughIncomeBody } from './types';

export const passThroughIncomeKeys = {
  all: ['pass-through-income'] as const,
  forReturn: (returnId: string) => [...passThroughIncomeKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/pass-through-income`;

export function listPassThroughIncome(returnId: string): Promise<PassThroughIncomeDto[]> {
  return apiGet<PassThroughIncomeDto[]>(base(returnId));
}

export function addPassThroughIncome(returnId: string, body: UpsertPassThroughIncomeBody): Promise<PassThroughIncomeDto> {
  return apiPost<PassThroughIncomeDto>(base(returnId), body);
}

export function deletePassThroughIncome(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
