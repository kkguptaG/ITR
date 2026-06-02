// features/foreign-source-income/api.ts — Schedule FSI/TR1 foreign-source income (list / add / delete).
//   GET/POST /returns/{id}/foreign-source-income   DELETE .../{id}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { ForeignSourceIncomeDto, UpsertForeignSourceIncomeBody } from './types';

export const foreignSourceIncomeKeys = {
  all: ['foreign-source-income'] as const,
  forReturn: (returnId: string) => [...foreignSourceIncomeKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/foreign-source-income`;

export function listForeignSourceIncome(returnId: string): Promise<ForeignSourceIncomeDto[]> {
  return apiGet<ForeignSourceIncomeDto[]>(base(returnId));
}

export function addForeignSourceIncome(returnId: string, body: UpsertForeignSourceIncomeBody): Promise<ForeignSourceIncomeDto> {
  return apiPost<ForeignSourceIncomeDto>(base(returnId), body);
}

export function deleteForeignSourceIncome(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
