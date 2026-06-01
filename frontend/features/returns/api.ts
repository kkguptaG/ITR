// ---------------------------------------------------------------------------
// features/returns/api.ts
// TanStack Query keys + thin fetchers for the Returns/Filing module.
// All calls go through lib/api (bearer + refresh-on-401 + RFC7807 → ApiError).
// ---------------------------------------------------------------------------

import { apiGet, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  CreateReturnBody,
  ItrSelectionVerdict,
  ItrSelectorInput,
  ReturnDetailDto,
  ReturnSummaryDto,
  SlabsResponse,
} from './types';

export interface ListReturnsParams {
  ay?: string;
  status?: string;
  itrType?: string;
  page?: number;
  pageSize?: number;
}

/** Centralised query keys so dashboard + list + mutations stay cache-consistent. */
export const returnsKeys = {
  all: ['returns'] as const,
  lists: () => [...returnsKeys.all, 'list'] as const,
  list: (params: ListReturnsParams) => [...returnsKeys.lists(), params] as const,
  detail: (id: string) => [...returnsKeys.all, 'detail', id] as const,
  activeAy: ['assessment-year', 'active'] as const,
};

/** GET /returns — the current user's returns, newest first, paged + filterable. */
export function listReturns(
  params: ListReturnsParams,
): Promise<PagedResult<ReturnSummaryDto>> {
  return apiGet<PagedResult<ReturnSummaryDto>>('/returns', { params });
}

/** POST /returns — create a draft for an assessment year (201 → full detail). */
export function createReturn(body: CreateReturnBody): Promise<ReturnDetailDto> {
  return apiPost<ReturnDetailDto>('/returns', body);
}

/** GET /returns/selector — stateless "which ITR form?" recommendation. */
export function selectItr(input: ItrSelectorInput): Promise<ItrSelectionVerdict> {
  return apiGet<ItrSelectionVerdict>('/returns/selector', { params: input });
}

/**
 * GET /tax/slabs — used only to learn the active assessment-year code for the
 * new-return dialog (there is no assessment-years endpoint). Anonymous-friendly.
 */
export function getActiveAssessmentYear(): Promise<SlabsResponse> {
  return apiGet<SlabsResponse>('/tax/slabs');
}
