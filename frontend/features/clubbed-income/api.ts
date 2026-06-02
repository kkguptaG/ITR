// features/clubbed-income/api.ts — Schedule SPI clubbed income (list / add / delete).
//   GET/POST /returns/{id}/clubbed-income   DELETE .../{id}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { ClubbedIncomeDto, UpsertClubbedIncomeBody } from './types';

export const clubbedIncomeKeys = {
  all: ['clubbed-income'] as const,
  forReturn: (returnId: string) => [...clubbedIncomeKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/clubbed-income`;

export function listClubbedIncome(returnId: string): Promise<ClubbedIncomeDto[]> {
  return apiGet<ClubbedIncomeDto[]>(base(returnId));
}

export function addClubbedIncome(returnId: string, body: UpsertClubbedIncomeBody): Promise<ClubbedIncomeDto> {
  return apiPost<ClubbedIncomeDto>(base(returnId), body);
}

export function deleteClubbedIncome(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
