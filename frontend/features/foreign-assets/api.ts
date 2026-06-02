// features/foreign-assets/api.ts — Schedule FA foreign bank accounts (list / add / delete).
//   GET/POST /returns/{id}/foreign-bank-accounts   DELETE .../{accId}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { ForeignBankAccountDto, UpsertForeignBankAccountBody } from './types';

export const foreignAssetsKeys = {
  all: ['foreign-assets'] as const,
  forReturn: (returnId: string) => [...foreignAssetsKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/foreign-bank-accounts`;

export function listForeignBankAccounts(returnId: string): Promise<ForeignBankAccountDto[]> {
  return apiGet<ForeignBankAccountDto[]>(base(returnId));
}

export function addForeignBankAccount(returnId: string, body: UpsertForeignBankAccountBody): Promise<ForeignBankAccountDto> {
  return apiPost<ForeignBankAccountDto>(base(returnId), body);
}

export function deleteForeignBankAccount(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
