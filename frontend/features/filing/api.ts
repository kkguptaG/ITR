// ---------------------------------------------------------------------------
// features/filing/api.ts
// TanStack Query keys + thin fetchers for the FILING WIZARD. Every call goes
// through lib/api (bearer + refresh-on-401 + RFC7807 -> ApiError). Routes use
// the colon ":verb" sub-resource convention exactly as the backend declares them.
// ---------------------------------------------------------------------------

import { api, apiGet, apiPatch, apiPost, API_BASE_URL } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  ReturnDetailDto,
  ReturnStatusDto,
} from '@/features/returns/types';
import type {
  ApproveExtractionRequest,
  ApproveExtractionResponse,
  AssignmentDto,
  BusinessIncomeDto,
  CapitalGainDto,
  CapitalGainImportRequest,
  CapitalGainImportResult,
  ParseCapitalGainDocumentRequest,
  CgInsightsResult,
  CompleteUploadRequest,
  ComputeRequest,
  ComputeResponse,
  CouponResultDto,
  CouponValidateRequest,
  CreateOrderRequest,
  CreateOrderResponse,
  DeductionDto,
  DocumentDto,
  ExtractionDto,
  GrandfatherFmvRecord,
  HousePropertyDto,
  IncomeSourceDto,
  InitiateUploadRequest,
  InitiateUploadResponse,
  PlanDto,
  RecommendationsRequest,
  RecommendationsResponse,
  SalaryDetailDto,
  SubmitReturnResponse,
  UpsertBusinessIncomeRequest,
  UpsertCapitalGainRequest,
  UpsertDeductionRequest,
  UpsertHousePropertyRequest,
  UpsertIncomeSourceRequest,
  UpsertSalaryRequest,
  ValidateReturnResponse,
  VerifyPaymentRequest,
  VerifyPaymentResponse,
} from './types';

/** Random idempotency key for money-moving POSTs (payments). */
function idempotencyKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

// --------------------------------------------------------------- query keys
export const filingKeys = {
  all: ['filing'] as const,
  detail: (id: string) => [...filingKeys.all, 'detail', id] as const,
  status: (id: string) => [...filingKeys.all, 'status', id] as const,
  salaries: (id: string) => [...filingKeys.all, 'salaries', id] as const,
  houses: (id: string) => [...filingKeys.all, 'house-property', id] as const,
  gains: (id: string) => [...filingKeys.all, 'capital-gains', id] as const,
  business: (id: string) => [...filingKeys.all, 'business-income', id] as const,
  deductions: (id: string) => [...filingKeys.all, 'deductions', id] as const,
  incomeSources: (id: string) => [...filingKeys.all, 'income-sources', id] as const,
  documents: (id: string) => [...filingKeys.all, 'documents', id] as const,
  extraction: (docId: string) => [...filingKeys.all, 'extraction', docId] as const,
  compute: (id: string) => [...filingKeys.all, 'compute', id] as const,
  regimeCompare: (id: string) => [...filingKeys.all, 'regime-compare', id] as const,
  recommendations: (id: string) => [...filingKeys.all, 'recommendations', id] as const,
  plans: ['filing', 'plans'] as const,
};

// --------------------------------------------------------------- return header
export const getReturn = (id: string) =>
  apiGet<ReturnDetailDto>(`/returns/${id}`);

export const getReturnStatus = (id: string) =>
  apiGet<ReturnStatusDto>(`/returns/${id}/status`);

export interface UpdateReturnBody {
  itrType?: string;
  regime?: string;
  answersJson?: string;
  tdsPaid?: number;
  tcsPaid?: number;
  advanceTaxPaid?: number;
  selfAssessmentTaxPaid?: number;
  broughtForwardHousePropertyLoss?: number;
  broughtForwardBusinessLoss?: number;
  broughtForwardShortTermCapitalLoss?: number;
  broughtForwardLongTermCapitalLoss?: number;
  broughtForwardAmtCredit?: number;
  relief89?: number;
  foreignIncomeDoublyTaxed?: number;
  foreignTaxPaid?: number;
  foreignDtaaApplies?: boolean;
  /** s.139 filing section: 'Original' | 'Belated' | 'Revised' | 'Updated'. */
  filingSection?: string;
  /** 15-digit acknowledgment number of the original return (revised/updated). */
  originalAcknowledgmentNumber?: string | null;
  /** Original return filing date, YYYY-MM-DD (revised/updated). */
  originalFilingDate?: string | null;
  /** ITR-U reason for updating ('1'..'7' | 'OTH'). */
  updatedReturnReason?: string;
  /** ITR-U time tier 1-4 (≤12/24/36/48 months → 25/50/60/70% additional tax). */
  updatedReturnTier?: number;
  /** Whether the original return was previously filed for this AY. */
  originalReturnPreviouslyFiled?: boolean;
  /** Tax already paid with the original return. */
  originalTaxPaid?: number;
}
export const updateReturn = (id: string, body: UpdateReturnBody) =>
  apiPatch<ReturnDetailDto>(`/returns/${id}`, body);

// --------------------------------------------------------------- salary
export const listSalaries = (id: string) =>
  apiGet<SalaryDetailDto[]>(`/returns/${id}/salary`);
export const addSalary = (id: string, body: UpsertSalaryRequest) =>
  apiPost<SalaryDetailDto>(`/returns/${id}/salary`, body);
export const updateSalary = (id: string, salaryId: string, body: UpsertSalaryRequest) =>
  apiPatch<SalaryDetailDto>(`/returns/${id}/salary/${salaryId}`, body);
export const deleteSalary = (id: string, salaryId: string) =>
  api.delete(`/returns/${id}/salary/${salaryId}`).then(() => undefined);

// --------------------------------------------------------------- house property
export const listHouseProperties = (id: string) =>
  apiGet<HousePropertyDto[]>(`/returns/${id}/house-property`);
export const addHouseProperty = (id: string, body: UpsertHousePropertyRequest) =>
  apiPost<HousePropertyDto>(`/returns/${id}/house-property`, body);
export const updateHouseProperty = (id: string, propertyId: string, body: UpsertHousePropertyRequest) =>
  apiPatch<HousePropertyDto>(`/returns/${id}/house-property/${propertyId}`, body);
export const deleteHouseProperty = (id: string, propertyId: string) =>
  api.delete(`/returns/${id}/house-property/${propertyId}`).then(() => undefined);

// --------------------------------------------------------------- capital gains
export const listCapitalGains = (id: string) =>
  apiGet<CapitalGainDto[]>(`/returns/${id}/capital-gains`);
export const addCapitalGain = (id: string, body: UpsertCapitalGainRequest) =>
  apiPost<CapitalGainDto>(`/returns/${id}/capital-gains`, body);
export const updateCapitalGain = (id: string, gainId: string, body: UpsertCapitalGainRequest) =>
  apiPatch<CapitalGainDto>(`/returns/${id}/capital-gains/${gainId}`, body);
export const deleteCapitalGain = (id: string, gainId: string) =>
  api.delete(`/returns/${id}/capital-gains/${gainId}`).then(() => undefined);
export const importCapitalGains = (id: string, body: CapitalGainImportRequest) =>
  apiPost<CapitalGainImportResult>(`/returns/${id}/capital-gains/import`, body);
export const getCapitalGainInsights = (id: string) =>
  apiGet<CgInsightsResult>(`/returns/${id}/capital-gains/insights`);
export const parseCapitalGainDocument = (id: string, body: ParseCapitalGainDocumentRequest) =>
  apiPost<CapitalGainImportResult>(`/returns/${id}/capital-gains/parse-document`, body);

// --------------------------------------------------------------- business income
export const listBusinessIncomes = (id: string) =>
  apiGet<BusinessIncomeDto[]>(`/returns/${id}/business-income`);
export const addBusinessIncome = (id: string, body: UpsertBusinessIncomeRequest) =>
  apiPost<BusinessIncomeDto>(`/returns/${id}/business-income`, body);
export const updateBusinessIncome = (id: string, businessId: string, body: UpsertBusinessIncomeRequest) =>
  apiPatch<BusinessIncomeDto>(`/returns/${id}/business-income/${businessId}`, body);
export const deleteBusinessIncome = (id: string, businessId: string) =>
  api.delete(`/returns/${id}/business-income/${businessId}`).then(() => undefined);

// --------------------------------------------------------------- other income sources
export const listIncomeSources = (id: string) =>
  apiGet<IncomeSourceDto[]>(`/returns/${id}/income-sources`);
export const addIncomeSource = (id: string, body: UpsertIncomeSourceRequest) =>
  apiPost<IncomeSourceDto>(`/returns/${id}/income-sources`, body);
export const updateIncomeSource = (id: string, sourceId: string, body: UpsertIncomeSourceRequest) =>
  apiPatch<IncomeSourceDto>(`/returns/${id}/income-sources/${sourceId}`, body);
export const deleteIncomeSource = (id: string, sourceId: string) =>
  api.delete(`/returns/${id}/income-sources/${sourceId}`).then(() => undefined);

// --------------------------------------------------------------- deductions
export const listDeductions = (id: string) =>
  apiGet<DeductionDto[]>(`/returns/${id}/deductions`);
export const addDeduction = (id: string, body: UpsertDeductionRequest) =>
  apiPost<DeductionDto>(`/returns/${id}/deductions`, body);
export const updateDeduction = (id: string, deductionId: string, body: UpsertDeductionRequest) =>
  apiPatch<DeductionDto>(`/returns/${id}/deductions/${deductionId}`, body);
export const deleteDeduction = (id: string, deductionId: string) =>
  api.delete(`/returns/${id}/deductions/${deductionId}`).then(() => undefined);

// --------------------------------------------------------------- tax engine
export const computeTax = (body: ComputeRequest) =>
  apiPost<ComputeResponse>('/tax/compute', body);
export const regimeCompare = (returnId: string) =>
  apiPost<ComputeResponse>('/tax/regime-compare', { returnId });
export const getRecommendations = (body: RecommendationsRequest) =>
  apiPost<RecommendationsResponse>('/tax/recommendations', body);

// --------------------------------------------------------------- documents
export const listDocuments = (returnId: string) =>
  apiGet<PagedResult<DocumentDto>>('/documents', { params: { returnId, pageSize: 100 } });

export const initiateUpload = (body: InitiateUploadRequest) =>
  apiPost<InitiateUploadResponse>('/documents:initiate-upload', body);

/**
 * PUT the raw bytes to the pre-signed upload URL with the echoed headers.
 *
 * The dev IFileStorage loopback returns an ABSOLUTE URL on our own API base
 * (e.g. http://localhost:5080/api/v1/documents/_local-upload?key=…) and that
 * route is [Authorize]-protected — so it must carry the bearer token. We detect
 * URLs that target our API base and route them through the axios instance
 * (baseURL + bearer + refresh). Only a genuinely external host (a real S3
 * pre-signed URL, which is self-authenticating) is PUT with a bare fetch.
 */
export async function uploadBytes(
  uploadUrl: string,
  uploadHeaders: Record<string, string>,
  file: File | Blob,
): Promise<void> {
  const headers = { 'Content-Type': file.type || 'application/octet-stream', ...uploadHeaders };

  // Same-origin-as-API (incl. the absolute dev loopback) → go through axios so the
  // bearer token is attached. We compute the path relative to the axios baseURL.
  const base = API_BASE_URL.replace(/\/+$/, '');
  if (uploadUrl.startsWith(base)) {
    const relative = uploadUrl.slice(base.length) || '/';
    await api.put(relative, file, { headers });
    return;
  }
  if (!/^https?:\/\//i.test(uploadUrl)) {
    // A relative path (already relative to the API base).
    await api.put(uploadUrl.replace(/^\/api\/v1/, ''), file, { headers });
    return;
  }
  // External pre-signed URL (real S3): PUT directly, no app credentials.
  await fetch(uploadUrl, { method: 'PUT', headers, body: file });
}

export const completeUpload = (id: string, body?: CompleteUploadRequest) =>
  apiPost<DocumentDto>(`/documents/${id}:complete`, body ?? {});

export const getExtraction = (id: string) =>
  apiGet<ExtractionDto>(`/documents/${id}/extraction`);

export const approveExtraction = (id: string, body?: ApproveExtractionRequest) =>
  apiPost<ApproveExtractionResponse>(`/documents/${id}/extraction:approve`, body ?? { mapToReturn: true });

// --------------------------------------------------------------- payments
export const getPlans = () => apiGet<PlanDto[]>('/pricing/plans');

export const validateCoupon = (body: CouponValidateRequest) =>
  apiPost<CouponResultDto>('/coupons:validate', body);

export const createOrder = (body: CreateOrderRequest) =>
  apiPost<CreateOrderResponse>('/payments/orders', body, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });

export const verifyPayment = (paymentId: string, body: VerifyPaymentRequest) =>
  apiPost<VerifyPaymentResponse>(`/payments/${paymentId}:verify`, body, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });

// --------------------------------------------------------------- lifecycle
export const validateReturn = (id: string) =>
  apiPost<ValidateReturnResponse>(`/returns/${id}:validate`, {});

export const submitReturn = (id: string) =>
  apiPost<SubmitReturnResponse>(`/returns/${id}:submit`, {}, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });

// --------------------------------------------------------------- securities reference
/** Type-ahead search of NSE symbols (with their 31-Jan-2018 FMV) for s.112A grandfathering. */
export const searchGrandfatherFmv = (q: string) =>
  apiGet<GrandfatherFmvRecord[]>(`/reference/grandfather-fmv?q=${encodeURIComponent(q)}`);

export type { AssignmentDto };
