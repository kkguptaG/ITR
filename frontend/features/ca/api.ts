// ---------------------------------------------------------------------------
// features/ca/api.ts
// TanStack Query keys + thin fetchers for the CA review workflow.
// All calls go through lib/api (bearer + refresh-on-401 + RFC7807 → ApiError).
// Endpoints (docs 04 §"CA Workflow"):
//   GET  /ca/queue
//   GET  /ca/assignments/{id}
//   POST /returns/{id}/review:approve
//   POST /returns/{id}/review:request-changes
// ---------------------------------------------------------------------------

import { apiGet, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  AssignmentDetailDto,
  AssignmentDto,
  QueueItemDto,
  ReviewActionRequest,
} from './types';

export interface QueueParams {
  page?: number;
  pageSize?: number;
}

/** Centralised query keys so the queue list + assignment detail stay cache-consistent. */
export const caKeys = {
  all: ['ca'] as const,
  queue: (params: QueueParams) => [...caKeys.all, 'queue', params] as const,
  assignment: (id: string) => [...caKeys.all, 'assignment', id] as const,
};

/** GET /ca/queue — the caller's CA work queue (assigned + firm pool), paged. */
export function getQueue(params: QueueParams): Promise<PagedResult<QueueItemDto>> {
  return apiGet<PagedResult<QueueItemDto>>('/ca/queue', { params });
}

/** GET /ca/assignments/{id} — the return summary + the full review comment history. */
export function getAssignment(id: string): Promise<AssignmentDetailDto> {
  return apiGet<AssignmentDetailDto>(`/ca/assignments/${id}`);
}

/** POST /returns/{returnId}/review:approve — sign off → ReadyToFile. */
export function approveReturn(
  returnId: string,
  body: ReviewActionRequest,
): Promise<AssignmentDto> {
  return apiPost<AssignmentDto>(`/returns/${returnId}/review:approve`, body);
}

/** POST /returns/{returnId}/review:request-changes — bounce back to the taxpayer. */
export function requestChanges(
  returnId: string,
  body: ReviewActionRequest,
): Promise<AssignmentDto> {
  return apiPost<AssignmentDto>(`/returns/${returnId}/review:request-changes`, body);
}
