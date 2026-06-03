// ---------------------------------------------------------------------------
// features/accounting/api.ts
// TanStack Query keys + thin fetchers for the Accounting module. All calls go
// through lib/api (bearer + refresh-on-401 + RFC7807 → ApiError).
//
// Backend routes:
//   GET/POST/PUT/DELETE /accounting/ledgers[/{id}]
//   POST  /accounting/bank-imports?fileName=&bankLedgerId=   (raw file body)
//   GET   /accounting/bank-imports[/{id}]
//   POST  /accounting/bank-imports/{id}:post                 (commit vouchers)
// ---------------------------------------------------------------------------

import { api, apiDelete, apiGet, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  BankImportDetailDto,
  BankImportDto,
  CreateLedgerBody,
  FinancialStatementsDto,
  LedgerDto,
  LedgerGroup,
  PostImportBody,
  PostImportResponse,
  UpdateLedgerBody,
} from './types';

export interface ListLedgersParams {
  group?: LedgerGroup;
  systemGenerated?: boolean;
  bank?: boolean;
}

export interface ListImportsParams {
  page?: number;
  pageSize?: number;
}

/** Centralised query keys so the chart of accounts + imports stay cache-consistent. */
export const accountingKeys = {
  all: ['accounting'] as const,
  ledgers: (params: ListLedgersParams = {}) =>
    [...accountingKeys.all, 'ledgers', params] as const,
  imports: (params: ListImportsParams = {}) =>
    [...accountingKeys.all, 'imports', params] as const,
  import: (id: string) => [...accountingKeys.all, 'import', id] as const,
  financialStatements: () => [...accountingKeys.all, 'financial-statements'] as const,
};

// ---- Financial statements (Balance Sheet + P&L derived from the books) ----

export function getFinancialStatements(): Promise<FinancialStatementsDto> {
  return apiGet<FinancialStatementsDto>('/accounting/financial-statements');
}

// ---- Chart of accounts ----------------------------------------------------

export function listLedgers(params: ListLedgersParams = {}): Promise<LedgerDto[]> {
  return apiGet<LedgerDto[]>('/accounting/ledgers', { params });
}

export function createLedger(body: CreateLedgerBody): Promise<LedgerDto> {
  return apiPost<LedgerDto>('/accounting/ledgers', body);
}

export async function updateLedger(id: string, body: UpdateLedgerBody): Promise<LedgerDto> {
  const { data } = await api.put<LedgerDto>(`/accounting/ledgers/${id}`, body);
  return data;
}

export function deleteLedger(id: string): Promise<void> {
  return apiDelete<void>(`/accounting/ledgers/${id}`);
}

// ---- Bank statement import ------------------------------------------------

export function listImports(params: ListImportsParams): Promise<PagedResult<BankImportDto>> {
  return apiGet<PagedResult<BankImportDto>>('/accounting/bank-imports', { params });
}

export function getImport(id: string): Promise<BankImportDetailDto> {
  return apiGet<BankImportDetailDto>(`/accounting/bank-imports/${id}`);
}

/**
 * Upload a statement in one call: the file is the raw request body; fileName and
 * the optional bank-ledger id ride on the query string. Goes through the authed
 * `api` instance so the bearer token is attached (unlike Documents' pre-signed PUT).
 */
export async function uploadStatement(
  file: File,
  bankLedgerId?: string | null,
): Promise<BankImportDetailDto> {
  const { data } = await api.post<BankImportDetailDto>(
    '/accounting/bank-imports',
    file,
    {
      params: { fileName: file.name, bankLedgerId: bankLedgerId || undefined },
      headers: { 'Content-Type': file.type || 'application/octet-stream' },
      timeout: 120_000,
    },
  );
  return data;
}

/** Commit reviewed lines: create adopted ledgers and post their vouchers. */
export function postImport(id: string, body: PostImportBody = {}): Promise<PostImportResponse> {
  return apiPost<PostImportResponse>(`/accounting/bank-imports/${id}:post`, body);
}

export interface PushToReturnResponse {
  rowsUpserted: number;
  message: string;
}

/** Push posted OtherIncome credit lines from this import to a tax return as income-source rows. */
export function pushImportToReturn(importId: string, returnId: string): Promise<PushToReturnResponse> {
  return apiPost<PushToReturnResponse>(
    `/accounting/bank-imports/${importId}:push-to-return`,
    undefined,
    { params: { returnId } },
  );
}

export function deleteImport(id: string): Promise<void> {
  return apiDelete<void>(`/accounting/bank-imports/${id}`);
}
