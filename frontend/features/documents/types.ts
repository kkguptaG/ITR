// ---------------------------------------------------------------------------
// features/documents/types.ts
// Wire types for the Documents module, mirroring the backend DTOs EXACTLY
// (ASP.NET Core, camelCase wire, `decimal` serialized as a JSON number).
//
// These are intentionally feature-local: the real contract in
// backend/src/TallyG.Tax.Api/Modules/Documents/DocumentDtos.cs is richer than
// the generic placeholders in lib/api-types.ts — e.g. `kind` is a string,
// the FK is `returnId` (not `taxReturnId`), extraction fields are { key,
// value, confidence }, and the upload response carries method + headers.
//
// Source of truth: backend Modules/Documents/DocumentDtos.cs +
// docs/architecture/05-ai-and-documents.md.
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime } from '@/lib/api-types';

/**
 * Document kinds the backend accepts (DocumentKind enum). Wider than the
 * lib/api-types placeholder — the extraction mock recognises all of these.
 */
export type DocumentKind =
  | 'Form16'
  | 'Form16A'
  | 'Form26AS'
  | 'AIS'
  | 'TIS'
  | 'BankStatement'
  | 'CapitalGainStmt'
  | 'SalarySlip'
  | 'GstData'
  | 'RentReceipt'
  | 'InvestmentProof'
  | 'Other';

/** Document lifecycle status (DocumentStatus enum). */
export type DocumentStatus =
  | 'Uploaded'
  | 'Scanning'
  | 'Extracting'
  | 'Extracted'
  | 'NeedsReview'
  | 'Verified'
  | 'Failed';

/** A document metadata row (the bytes live in object storage). */
export interface DocumentDto {
  id: Guid;
  returnId: Guid | null;
  kind: DocumentKind;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: DocumentStatus;
  sha256: string | null;
  hasExtraction: boolean;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

// --- :initiate-upload -------------------------------------------------------

/** POST /documents:initiate-upload body. `kind` is a DocumentKind name. */
export interface InitiateUploadBody {
  kind: DocumentKind;
  fileName: string;
  contentType: string;
  returnId?: Guid | null;
}

/**
 * POST /documents:initiate-upload response. The client PUTs the raw bytes to
 * `uploadUrl` using `uploadMethod`, replaying `uploadHeaders`, then calls
 * POST /documents/{id}:complete.
 */
export interface InitiateUploadResponse {
  documentId: Guid;
  uploadUrl: string;
  uploadMethod: string;
  uploadHeaders: Record<string, string>;
  expiresAt: IsoDateTime;
}

// --- :complete --------------------------------------------------------------

/** POST /documents/{id}:complete body (optional integrity hints). */
export interface CompleteUploadBody {
  eTag?: string | null;
  sha256?: string | null;
}

// --- extraction -------------------------------------------------------------

/** One extracted field with provenance + confidence (flattened from FieldsJson). */
export interface ExtractedFieldDto {
  key: string;
  value: string | null;
  /** 0..1; null for non-scored fields. */
  confidence: number | null;
}

/** The extraction result for a document, including the parsed field map. */
export interface ExtractionDto {
  id: Guid;
  documentId: Guid;
  docClass: string;
  status: DocumentStatus;
  confidenceScore: number | null;
  fieldsJson: string;
  fields: ExtractedFieldDto[];
  needsReview: boolean;
  reviewedByUserId: Guid | null;
  reviewedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

// --- extraction:approve -----------------------------------------------------

/**
 * POST /documents/{id}/extraction:approve body. When `mapToReturn` is true and
 * the document is linked to a return, verified fields are projected onto the
 * return. `fieldOverrides` lets the reviewer correct values (HITL) — keys must
 * match the canonical extraction field keys.
 */
export interface ApproveExtractionBody {
  mapToReturn?: boolean;
  fieldOverrides?: Record<string, string> | null;
}

/** Result of approving an extraction. */
export interface ApproveExtractionResponse {
  extraction: ExtractionDto;
  incomeSourcesUpserted: number;
  deductionsUpserted: number;
}
