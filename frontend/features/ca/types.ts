// ---------------------------------------------------------------------------
// features/ca/types.ts
// Wire types for the in-house CA review workflow. These mirror the backend
// records in Api/Modules/Ca/CaDtos.cs (camelCase on the wire) — which are richer
// than the generic CaAssignmentDto/ReviewDto in lib/api-types, so we keep
// feature-local types that match the actual /ca endpoints exactly.
// Source: docs/architecture/04-api-and-auth.md §"CA Workflow".
// ---------------------------------------------------------------------------

import type {
  AssignmentStatus,
  DecimalString,
  Guid,
  IsoDateTime,
  ItrType,
  Regime,
  ReturnStatus,
  ReviewOutcome,
} from '@/lib/api-types';

/** Compact taxpayer-return snapshot shown to the CA (queue + assignment detail). */
export interface CaReturnSummaryDto {
  returnId: Guid;
  userId: Guid;
  taxpayerName?: string | null;
  assessmentYear?: string | null;
  itrType?: ItrType | null;
  status: ReturnStatus;
  regime?: Regime | null;
  /** Positive = refund due, negative = payable. Null when not yet computed. */
  refundOrPayable?: DecimalString | null;
  createdAt: IsoDateTime;
  submittedAt?: IsoDateTime | null;
}

/** One row in the CA work queue. */
export interface QueueItemDto {
  assignmentId?: Guid | null;
  status: AssignmentStatus;
  caUserId?: Guid | null;
  priority: number;
  slaDueAt?: IsoDateTime | null;
  assignedAt?: IsoDateTime | null;
  /** True for items in the firm's unassigned UnderCaReview pool (CaFirmAdmin). */
  isUnassignedPool: boolean;
  return: CaReturnSummaryDto;
}

/** A single comment in the review history (CA decisions over time). */
export interface ReviewCommentDto {
  id: Guid;
  outcome: ReviewOutcome;
  comments?: string | null;
  caUserId: Guid;
  caName?: string | null;
  createdAt: IsoDateTime;
}

/** An assignment with its return summary and full comment history. */
export interface AssignmentDetailDto {
  assignmentId: Guid;
  status: AssignmentStatus;
  caUserId: Guid;
  assignedByUserId: Guid;
  assignmentType: string;
  priority: number;
  slaDueAt?: IsoDateTime | null;
  assignedAt: IsoDateTime;
  completedAt?: IsoDateTime | null;
  return: CaReturnSummaryDto;
  comments: ReviewCommentDto[];
}

/** Returned by the review actions (approve / request-changes). */
export interface AssignmentDto {
  assignmentId: Guid;
  taxReturnId: Guid;
  caUserId: Guid;
  status: AssignmentStatus;
  priority: number;
  slaDueAt?: IsoDateTime | null;
  assignedAt: IsoDateTime;
  completedAt?: IsoDateTime | null;
  returnStatus: ReturnStatus;
}

/** Body for review:approve / review:request-changes. */
export interface ReviewActionRequest {
  comments?: string | null;
}
