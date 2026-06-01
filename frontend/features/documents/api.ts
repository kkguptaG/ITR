// ---------------------------------------------------------------------------
// features/documents/api.ts
// TanStack Query keys + thin fetchers for the Documents module.
// All calls go through lib/api (bearer + refresh-on-401 + RFC7807 → ApiError),
// EXCEPT the raw byte PUT, which goes to the (possibly off-origin) pre-signed
// upload URL with a bare axios call — see uploadBytes().
//
// Backend routes (":verb" sub-resource convention, docs 04 / D-3):
//   POST /documents:initiate-upload
//   PUT  <uploadUrl>                     (bytes; loopback in dev, S3 in prod)
//   POST /documents/{id}:complete
//   GET  /documents                      (paged, filterable)
//   GET  /documents/{id}/extraction
//   POST /documents/{id}/extraction:approve
//   GET  /documents/{id}:download        (file stream → blob)
// ---------------------------------------------------------------------------

import axios from 'axios';
import { api, apiGet, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  ApproveExtractionBody,
  ApproveExtractionResponse,
  CompleteUploadBody,
  DocumentDto,
  DocumentKind,
  DocumentStatus,
  ExtractionDto,
  InitiateUploadBody,
  InitiateUploadResponse,
} from './types';

export interface ListDocumentsParams {
  page?: number;
  pageSize?: number;
  returnId?: string;
  kind?: DocumentKind;
  status?: DocumentStatus;
}

/** Centralised query keys so the list + detail + extraction stay cache-consistent. */
export const documentsKeys = {
  all: ['documents'] as const,
  lists: () => [...documentsKeys.all, 'list'] as const,
  list: (params: ListDocumentsParams) => [...documentsKeys.lists(), params] as const,
  detail: (id: string) => [...documentsKeys.all, 'detail', id] as const,
  extraction: (id: string) => [...documentsKeys.all, 'extraction', id] as const,
};

/** GET /documents — the caller's documents (tenant + ownership scoped), newest first. */
export function listDocuments(
  params: ListDocumentsParams,
): Promise<PagedResult<DocumentDto>> {
  return apiGet<PagedResult<DocumentDto>>('/documents', { params });
}

/** GET /documents/{id} — single document metadata. */
export function getDocument(id: string): Promise<DocumentDto> {
  return apiGet<DocumentDto>(`/documents/${id}`);
}

/** GET /documents/{id}/extraction — parsed fields + per-field confidence. */
export function getExtraction(id: string): Promise<ExtractionDto> {
  return apiGet<ExtractionDto>(`/documents/${id}/extraction`);
}

/** Step 1: create the document row and mint an upload URL. */
export function initiateUpload(
  body: InitiateUploadBody,
): Promise<InitiateUploadResponse> {
  return apiPost<InitiateUploadResponse>('/documents:initiate-upload', body);
}

/**
 * Step 2: PUT the raw bytes to the pre-signed upload URL.
 *
 * The URL may be absolute (S3 in prod) or relative to the API origin (the dev
 * loopback). We resolve it against the configured API origin and use a bare
 * axios instance (no bearer/refresh interceptors) because the upload target is
 * the object store, not our API — exactly the production contract.
 */
export async function uploadBytes(
  init: InitiateUploadResponse,
  file: File,
): Promise<void> {
  const url = resolveUploadUrl(init.uploadUrl);
  const method = (init.uploadMethod || 'PUT').toUpperCase();
  await axios.request({
    url,
    method: method as 'PUT' | 'POST',
    data: file,
    headers: {
      // Echo the headers the API told us to replay; default the content type.
      'Content-Type': file.type || 'application/octet-stream',
      ...init.uploadHeaders,
    },
    // Object-store PUTs can be large/slow; relax the default 30s API timeout.
    timeout: 120_000,
  });
}

/** Step 3: complete the upload; the API runs extraction synchronously (stub). */
export function completeUpload(
  id: string,
  body: CompleteUploadBody = {},
): Promise<DocumentDto> {
  return apiPost<DocumentDto>(`/documents/${id}:complete`, body);
}

/** HITL accept: verify the extraction and (optionally) map fields onto the return. */
export function approveExtraction(
  id: string,
  body: ApproveExtractionBody = {},
): Promise<ApproveExtractionResponse> {
  return apiPost<ApproveExtractionResponse>(`/documents/${id}/extraction:approve`, body);
}

/**
 * GET /documents/{id}:download — fetch the stored file as a Blob (goes through
 * the API so the bearer token + RBAC apply). Returns the blob + a best-effort
 * filename parsed from Content-Disposition.
 */
export async function downloadDocument(
  id: string,
): Promise<{ blob: Blob; fileName: string | null }> {
  const res = await api.get(`/documents/${id}:download`, { responseType: 'blob' });
  const disposition = res.headers['content-disposition'] as string | undefined;
  return { blob: res.data as Blob, fileName: parseFileName(disposition) };
}

// ----------------------------------------------------------------- helpers

/** Resolve a (possibly relative) pre-signed upload URL against the API origin. */
function resolveUploadUrl(uploadUrl: string): string {
  if (/^https?:\/\//i.test(uploadUrl)) return uploadUrl;
  const base = api.defaults.baseURL ?? '';
  try {
    // baseURL is ".../api/v1"; the loopback URL is rooted at the host, so
    // resolve relative to the API origin (drop the /api/v1 path segment).
    const origin = new URL(base, window.location.origin).origin;
    return new URL(uploadUrl, origin).toString();
  } catch {
    return uploadUrl;
  }
}

/** Pull a filename out of a Content-Disposition header, if present. */
function parseFileName(disposition: string | undefined): string | null {
  if (!disposition) return null;
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(disposition);
  if (star?.[1]) {
    try {
      return decodeURIComponent(star[1].replace(/"/g, '').trim());
    } catch {
      /* fall through */
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain?.[1]?.trim() ?? null;
}
