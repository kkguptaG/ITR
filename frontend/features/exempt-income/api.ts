// features/exempt-income/api.ts — Schedule EI exempt income (list / add / delete).
//   GET/POST /returns/{id}/exempt-income   DELETE .../{id}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { ExemptIncomeDto, UpsertExemptIncomeBody } from './types';

export const exemptIncomeKeys = {
  all: ['exempt-income'] as const,
  forReturn: (returnId: string) => [...exemptIncomeKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/exempt-income`;

export function listExemptIncome(returnId: string): Promise<ExemptIncomeDto[]> {
  return apiGet<ExemptIncomeDto[]>(base(returnId));
}

export function addExemptIncome(returnId: string, body: UpsertExemptIncomeBody): Promise<ExemptIncomeDto> {
  return apiPost<ExemptIncomeDto>(base(returnId), body);
}

export function deleteExemptIncome(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
