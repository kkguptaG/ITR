// ---------------------------------------------------------------------------
// features/bank-accounts/api.ts
// TanStack Query keys + thin fetchers for the BankAccounts module. All calls go
// through lib/api (bearer + refresh-on-401 + RFC7807 → ApiError).
//
// Backend routes:
//   GET    /bank-accounts
//   POST   /bank-accounts
//   POST   /bank-accounts/{id}:use-for-refund
//   DELETE /bank-accounts/{id}
//   GET    /ifsc/{code}                         (404 when not in the master)
// ---------------------------------------------------------------------------

import { ApiError, apiDelete, apiGet, apiPost } from '@/lib/api';
import type { BankAccountDto, IfscRecord, UpsertBankAccountBody } from './types';

/** Centralised query keys so the list + IFSC lookups stay cache-consistent. */
export const bankAccountKeys = {
  all: ['bank-accounts'] as const,
  list: () => [...bankAccountKeys.all, 'list'] as const,
  ifsc: (code: string) => ['ifsc', code] as const,
};

export function listBankAccounts(): Promise<BankAccountDto[]> {
  return apiGet<BankAccountDto[]>('/bank-accounts');
}

export function addBankAccount(body: UpsertBankAccountBody): Promise<BankAccountDto> {
  return apiPost<BankAccountDto>('/bank-accounts', body);
}

export function setBankAccountForRefund(id: string): Promise<BankAccountDto> {
  return apiPost<BankAccountDto>(`/bank-accounts/${id}:use-for-refund`);
}

export function deleteBankAccount(id: string): Promise<void> {
  return apiDelete<void>(`/bank-accounts/${id}`);
}

/** Resolve an IFSC to its bank + branch; null when the code isn't in the master (404). */
export async function lookupIfsc(code: string): Promise<IfscRecord | null> {
  try {
    return await apiGet<IfscRecord>(`/ifsc/${encodeURIComponent(code)}`);
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) {
      return null;
    }
    throw e;
  }
}
