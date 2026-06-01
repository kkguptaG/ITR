// ---------------------------------------------------------------------------
// features/filing/itr-json.ts
// Offline-filing ITR JSON data layer (pre-ERI): generate the ITD-format JSON,
// validate it, list saved artifacts, and download the file to upload on the
// Income Tax portal. Mirrors the EReturn backend module. Co-located so it adds
// to the filing feature without touching the shared api.ts / types.ts.
// ---------------------------------------------------------------------------

import { api, apiGet, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';

export interface ValidationIssue {
  severity: string; // "error" | "warning"
  code: string;
  path: string;
  message: string;
  suggestion: string;
}

export interface ValidationReport {
  isValid: boolean;
  errorCount: number;
  warningCount: number;
  issues: ValidationIssue[];
  notice: string;
}

export interface ItrJsonArtifact {
  id: string;
  returnId: string;
  assessmentYear: string;
  itrType: string; // "ITR1" | "ITR4" | ...
  schemaVersion: string;
  status: string; // "Generated" | "Valid" | "Invalid"
  isValid: boolean;
  errorCount: number;
  warningCount: number;
  fileName: string;
  sizeBytes: number;
  jsonHash: string | null;
  generatedAt: string;
  validatedAt: string | null;
}

export interface GenerateItrJsonResponse {
  artifact: ItrJsonArtifact;
  validation: ValidationReport;
}

/** POST /returns/{id}/itr-json:generate — build (and auto-validate) the ITR JSON. */
export const generateItrJson = (returnId: string) =>
  apiPost<GenerateItrJsonResponse>(`/returns/${returnId}/itr-json:generate`, {});

/** POST /itr-json/{fileId}:validate — re-run the pre-upload checks. */
export const validateItrJson = (fileId: string) =>
  apiPost<ValidationReport>(`/itr-json/${fileId}:validate`, {});

/** GET /returns/{id}/itr-json — saved artifact(s) for a return (latest first). */
export const listItrJsonForReturn = (returnId: string) =>
  apiGet<ItrJsonArtifact[]>(`/returns/${returnId}/itr-json`);

/** GET /itr-json/{fileId}/report — the LAST stored validation report (issues + suggestions), no re-run. */
export const getItrJsonReport = (fileId: string) =>
  apiGet<ValidationReport>(`/itr-json/${fileId}/report`);

/** GET /itr-json — the user's full "ready to file" list across all returns (paged). */
export const listMyItrJson = (page = 1, pageSize = 50) =>
  apiGet<PagedResult<ItrJsonArtifact>>('/itr-json', { params: { page, pageSize } });

/** GET /itr-json/{fileId}:download — authenticated blob download of the .json file. */
export async function downloadItrJson(fileId: string, fallbackName = `itr-${fileId}.json`): Promise<void> {
  const res = await api.get(`/itr-json/${fileId}:download`, { responseType: 'blob' });
  const blob = res.data as Blob;
  const disposition = (res.headers as Record<string, string>)['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1]) : fallbackName;

  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
