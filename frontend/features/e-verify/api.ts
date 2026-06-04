// ---------------------------------------------------------------------------
// features/e-verify/api.ts
// TanStack Query keys + thin fetchers for post-filing e-verification. Routes use
// the colon ":verb" sub-resource convention exactly as the backend declares them.
// ---------------------------------------------------------------------------

import { apiGet, apiPost } from '@/lib/api';
import type {
  EVerificationConfirmRequest,
  EVerificationStartRequest,
  EVerificationStartResponse,
  EVerificationStatusDto,
} from './types';

// --------------------------------------------------------------- query keys
export const eVerifyKeys = {
  all: ['e-verify'] as const,
  status: (returnId: string) => [...eVerifyKeys.all, 'status', returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/e-verify`;

// --------------------------------------------------------------- fetchers
export const getEVerifyStatus = (returnId: string) =>
  apiGet<EVerificationStatusDto>(base(returnId));

export const startEVerify = (returnId: string, body: EVerificationStartRequest) =>
  apiPost<EVerificationStartResponse>(`${base(returnId)}:start`, body);

export const confirmEVerify = (returnId: string, body: EVerificationConfirmRequest) =>
  apiPost<EVerificationStatusDto>(`${base(returnId)}:confirm`, body);
