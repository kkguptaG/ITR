// ---------------------------------------------------------------------------
// features/admin/types.ts
// TypeScript types for the back-office (Admin/Ops/SuperAdmin) console.
// These MIRROR the backend Admin module DTOs (camelCase on the wire):
//   • Modules/Admin/Analytics/AnalyticsDtos.cs
//   • Modules/Admin/Users/AdminUserDtos.cs
//   • Modules/Admin/Returns/AdminReturnDtos.cs
//   • Modules/Admin/Crm/LeadDtos.cs
//   • Modules/Admin/Audit/AuditDtos.cs
// Keep in sync with those records so client/server contracts never drift.
// ---------------------------------------------------------------------------

import type {
  AssignmentStatus,
  DocumentKind,
  DocumentStatus,
  Guid,
  IsoDateTime,
  ItrType,
  LeadStage,
  Regime,
  ReturnStatus,
} from '@/lib/api-types';

// ---- Shared -----------------------------------------------------------------

/** Account state for a user (mirrors Domain.Enums.UserStatus). */
export type UserStatus = 'Active' | 'Locked' | 'Disabled' | 'Deleted';

// ---- Analytics --------------------------------------------------------------

/** GET /admin/analytics/overview — headline KPIs for the console home. */
export interface AnalyticsOverviewDto {
  totalUsers: number;
  activeUsers: number;
  totalReturns: number;
  returnsInProgress: number;
  returnsFiled: number;
  returnsUnderCaReview: number;
  lifetimeRevenueNet: string; // decimal string
  revenueThisMonthNet: string;
  paidPayments: number;
  documentsAwaitingReview: number;
  openLeads: number;
  generatedAtUtc: IsoDateTime;
}

/** One bucket of revenue in the requested period grain. */
export interface RevenuePointDto {
  period: string; // "2026-05" (month) | "2026-05-31" (day)
  grossAmount: string;
  gstAmount: string;
  netAmount: string;
  paymentCount: number;
}

export type RevenueGranularity = 'day' | 'week' | 'month';

/** GET /admin/analytics/revenue — series plus totals. */
export interface RevenueReportDto {
  granularity: RevenueGranularity;
  fromUtc: IsoDateTime;
  toUtc: IsoDateTime;
  totalGross: string;
  totalGst: string;
  totalNet: string;
  totalPayments: number;
  series: RevenuePointDto[];
}

/** A labelled count (status / ITR-type / regime breakdowns). */
export interface CountBucketDto {
  key: string;
  count: number;
}

/** GET /admin/analytics/filings — filing funnel counts. */
export interface FilingsReportDto {
  totalReturns: number;
  filedReturns: number;
  byStatus: CountBucketDto[];
  byItrType: CountBucketDto[];
  byRegime: CountBucketDto[];
}

// ---- Users ------------------------------------------------------------------

/** One row in the admin user list (GET /admin/users). */
export interface AdminUserListItemDto {
  id: Guid;
  tenantId: Guid;
  fullName: string;
  email?: string | null;
  mobile?: string | null;
  panMasked?: string | null;
  status: UserStatus;
  emailVerified: boolean;
  mobileVerified: boolean;
  roles: string[];
  lastLoginAt?: IsoDateTime | null;
  createdAt: IsoDateTime;
}

/** Full admin view of a single user (GET /admin/users/{id}). */
export interface AdminUserDetailDto {
  id: Guid;
  tenantId: Guid;
  fullName: string;
  email?: string | null;
  mobile?: string | null;
  panMasked?: string | null;
  status: UserStatus;
  emailVerified: boolean;
  mobileVerified: boolean;
  roles: string[];
  returnsCount: number;
  paymentsCount: number;
  lastLoginAt?: IsoDateTime | null;
  createdAt: IsoDateTime;
}

/** PATCH /admin/users/{id}:status body. */
export interface UpdateUserStatusRequest {
  status: UserStatus;
  reason?: string | null;
}

/** POST /admin/users/{id}/roles body — grant ("assign") or revoke ("remove"). */
export interface ModifyUserRoleRequest {
  role: string;
  action: 'assign' | 'remove';
}

// ---- Returns board ----------------------------------------------------------

/** One card on the admin returns board (GET /admin/returns). */
export interface AdminReturnListItemDto {
  id: Guid;
  tenantId: Guid;
  userId: Guid;
  taxpayerName?: string | null;
  assessmentYear?: string | null;
  itrType?: ItrType | null;
  status: ReturnStatus;
  regime?: Regime | null;
  filingMode: string;
  refundOrPayable?: string | null;
  assignedCaUserId?: Guid | null;
  assignedCaName?: string | null;
  assignmentStatus?: AssignmentStatus | null;
  createdAt: IsoDateTime;
  submittedAt?: IsoDateTime | null;
}

/** POST /admin/returns/{id}:assign-ca body. */
export interface AssignReturnToCaRequest {
  caUserId: Guid;
  priority?: number | null;
}

/** Result of POST /admin/returns/{id}:assign-ca. */
export interface AdminAssignmentResultDto {
  assignmentId: Guid;
  taxReturnId: Guid;
  caUserId: Guid;
  status: AssignmentStatus;
  priority: number;
  slaDueAt?: IsoDateTime | null;
  assignedAt: IsoDateTime;
  returnStatus: ReturnStatus;
}

/** One document awaiting human review (GET /admin/doc-verification-queue). */
export interface DocVerificationQueueItemDto {
  documentId: Guid;
  tenantId: Guid;
  userId: Guid;
  ownerName?: string | null;
  taxReturnId?: Guid | null;
  kind: DocumentKind;
  fileName: string;
  status: DocumentStatus;
  extractionConfidence?: number | null;
  extractionId?: Guid | null;
  createdAt: IsoDateTime;
}

// ---- CRM leads --------------------------------------------------------------

/** List/detail projection of a lead. */
export interface LeadDto {
  id: Guid;
  tenantId?: Guid | null;
  name: string;
  email?: string | null;
  mobile?: string | null;
  source?: string | null;
  stage: LeadStage;
  ownerUserId?: Guid | null;
  convertedUserId?: Guid | null;
  score: number;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

/** A single CRM activity in a lead's timeline. */
export interface LeadActivityDto {
  id: Guid;
  type: string;
  notes?: string | null;
  performedByUserId?: Guid | null;
  createdAt: IsoDateTime;
}

/** A lead with its full activity timeline (GET /admin/leads/{id}). */
export interface LeadDetailDto {
  lead: LeadDto;
  activities: LeadActivityDto[];
}

/** One stage column in the pipeline view. */
export interface PipelineStageDto {
  stage: LeadStage;
  count: number;
  leads: LeadDto[];
}

/** GET /admin/leads/pipeline response — the kanban grouped by stage. */
export interface PipelineDto {
  totalLeads: number;
  stages: PipelineStageDto[];
}

/** POST /admin/leads body. */
export interface CreateLeadRequest {
  name: string;
  email?: string | null;
  mobile?: string | null;
  source?: string | null;
  ownerUserId?: Guid | null;
}

/** PATCH /admin/leads/{id}:stage body. */
export interface UpdateLeadStageRequest {
  stage: LeadStage;
  note?: string | null;
}

/** POST /admin/leads/{id}/activities body. */
export interface AddLeadActivityRequest {
  type: string;
  notes?: string | null;
}

// ---- Audit ------------------------------------------------------------------

/** One audit-trail entry (GET /admin/audit). */
export interface AdminAuditLogDto {
  id: Guid;
  tenantId?: Guid | null;
  actorUserId?: Guid | null;
  actorName?: string | null;
  action: string;
  entityType: string;
  entityId?: Guid | null;
  dataJson: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: IsoDateTime;
}
