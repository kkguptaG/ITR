// ---------------------------------------------------------------------------
// features/admin/api.ts
// TanStack Query keys + thin fetchers for the back-office (Admin) console.
// All calls go through lib/api (bearer + refresh-on-401 + RFC7807 -> ApiError).
//
// Route conventions (Decision Log D-3): action sub-resources use the ":verb"
// suffix, e.g. PATCH /admin/users/{id}:status, POST /admin/returns/{id}:assign-ca,
// PATCH /admin/leads/{id}:stage. The colon is a literal path char here (not an
// encoded segment), which matches the backend [HttpPatch("{id:guid}:status")].
// ---------------------------------------------------------------------------

import { apiGet, apiPatch, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  AddLeadActivityRequest,
  AdminAssignmentResultDto,
  AdminAuditLogDto,
  AdminReturnListItemDto,
  AdminUserDetailDto,
  AdminUserListItemDto,
  AnalyticsOverviewDto,
  AssignReturnToCaRequest,
  CreateLeadRequest,
  DocVerificationQueueItemDto,
  FilingsReportDto,
  LeadActivityDto,
  LeadDetailDto,
  LeadDto,
  ModifyUserRoleRequest,
  PipelineDto,
  RevenueGranularity,
  RevenueReportDto,
  UpdateLeadStageRequest,
  UpdateUserStatusRequest,
} from './types';

// ===========================================================================
// Query keys — one tree so list/detail/mutation invalidation stays consistent.
// ===========================================================================
export const adminKeys = {
  all: ['admin'] as const,

  overview: () => [...adminKeys.all, 'overview'] as const,
  revenue: (granularity: RevenueGranularity) =>
    [...adminKeys.all, 'revenue', granularity] as const,
  filings: () => [...adminKeys.all, 'filings'] as const,

  users: () => [...adminKeys.all, 'users'] as const,
  userList: (params: ListUsersParams) => [...adminKeys.users(), 'list', params] as const,
  userDetail: (id: string) => [...adminKeys.users(), 'detail', id] as const,

  returns: () => [...adminKeys.all, 'returns'] as const,
  returnList: (params: ListReturnsParams) =>
    [...adminKeys.returns(), 'list', params] as const,
  docQueue: (params: PageParams) => [...adminKeys.returns(), 'doc-queue', params] as const,

  leads: () => [...adminKeys.all, 'leads'] as const,
  leadPipeline: () => [...adminKeys.leads(), 'pipeline'] as const,
  leadList: (params: ListLeadsParams) => [...adminKeys.leads(), 'list', params] as const,
  leadDetail: (id: string) => [...adminKeys.leads(), 'detail', id] as const,

  audit: () => [...adminKeys.all, 'audit'] as const,
  auditList: (params: ListAuditParams) => [...adminKeys.audit(), 'list', params] as const,
};

export interface PageParams {
  page?: number;
  pageSize?: number;
}

// ===========================================================================
// Analytics
// ===========================================================================
export function getOverview(): Promise<AnalyticsOverviewDto> {
  return apiGet<AnalyticsOverviewDto>('/admin/analytics/overview');
}

export function getRevenue(granularity: RevenueGranularity): Promise<RevenueReportDto> {
  return apiGet<RevenueReportDto>('/admin/analytics/revenue', { params: { granularity } });
}

export function getFilings(): Promise<FilingsReportDto> {
  return apiGet<FilingsReportDto>('/admin/analytics/filings');
}

// ===========================================================================
// Users
// ===========================================================================
export interface ListUsersParams extends PageParams {
  search?: string;
}

export function listUsers(
  params: ListUsersParams,
): Promise<PagedResult<AdminUserListItemDto>> {
  return apiGet<PagedResult<AdminUserListItemDto>>('/admin/users', { params });
}

export function getUser(id: string): Promise<AdminUserDetailDto> {
  return apiGet<AdminUserDetailDto>(`/admin/users/${id}`);
}

export function setUserStatus(
  id: string,
  body: UpdateUserStatusRequest,
): Promise<AdminUserDetailDto> {
  return apiPatch<AdminUserDetailDto>(`/admin/users/${id}:status`, body);
}

export function modifyUserRole(
  id: string,
  body: ModifyUserRoleRequest,
): Promise<AdminUserDetailDto> {
  return apiPost<AdminUserDetailDto>(`/admin/users/${id}/roles`, body);
}

// ===========================================================================
// Returns board + doc-verification queue
// ===========================================================================
export interface ListReturnsParams extends PageParams {
  status?: string;
}

export function listAdminReturns(
  params: ListReturnsParams,
): Promise<PagedResult<AdminReturnListItemDto>> {
  return apiGet<PagedResult<AdminReturnListItemDto>>('/admin/returns', { params });
}

export function assignReturnToCa(
  id: string,
  body: AssignReturnToCaRequest,
): Promise<AdminAssignmentResultDto> {
  return apiPost<AdminAssignmentResultDto>(`/admin/returns/${id}:assign-ca`, body);
}

export function getDocVerificationQueue(
  params: PageParams,
): Promise<PagedResult<DocVerificationQueueItemDto>> {
  return apiGet<PagedResult<DocVerificationQueueItemDto>>('/admin/doc-verification-queue', {
    params,
  });
}

// ===========================================================================
// CRM leads
// ===========================================================================
export interface ListLeadsParams extends PageParams {
  stage?: string;
  search?: string;
}

export function getPipeline(perStage = 25): Promise<PipelineDto> {
  return apiGet<PipelineDto>('/admin/leads/pipeline', { params: { perStage } });
}

export function listLeads(params: ListLeadsParams): Promise<PagedResult<LeadDto>> {
  return apiGet<PagedResult<LeadDto>>('/admin/leads', { params });
}

export function getLead(id: string): Promise<LeadDetailDto> {
  return apiGet<LeadDetailDto>(`/admin/leads/${id}`);
}

export function createLead(body: CreateLeadRequest): Promise<LeadDetailDto> {
  return apiPost<LeadDetailDto>('/admin/leads', body);
}

export function changeLeadStage(
  id: string,
  body: UpdateLeadStageRequest,
): Promise<LeadDetailDto> {
  return apiPatch<LeadDetailDto>(`/admin/leads/${id}:stage`, body);
}

export function addLeadActivity(
  id: string,
  body: AddLeadActivityRequest,
): Promise<LeadActivityDto> {
  return apiPost<LeadActivityDto>(`/admin/leads/${id}/activities`, body);
}

// ===========================================================================
// Audit log
// ===========================================================================
export interface ListAuditParams extends PageParams {
  actorUserId?: string;
  entityType?: string;
  entityId?: string;
  action?: string;
}

export function listAudit(
  params: ListAuditParams,
): Promise<PagedResult<AdminAuditLogDto>> {
  return apiGet<PagedResult<AdminAuditLogDto>>('/admin/audit', { params });
}
