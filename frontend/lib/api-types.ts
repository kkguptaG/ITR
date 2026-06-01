// ---------------------------------------------------------------------------
// api-types.ts
// TypeScript types mirroring the backend DTOs (ASP.NET Core, camelCase wire).
// Feature agents import from here so client/server contracts never drift.
// Source of truth: docs/architecture 02 (schema), 04 (api/auth), task contract.
// ---------------------------------------------------------------------------

// ---- Primitives ----------------------------------------------------------
export type Guid = string;
/** Money is serialized as a string decimal on the wire ("125000.00") to keep
 *  NUMERIC(14,2) precision across JS. Helpers in lib/format parse to number. */
export type DecimalString = string;
/** ISO-8601 UTC, e.g. "2025-07-31T18:30:00Z". */
export type IsoDateTime = string;

// ---- Enums (mirror Domain/Enums) -----------------------------------------
export type ItrType = 'ITR1' | 'ITR2' | 'ITR3' | 'ITR4';

export type ReturnStatus =
  | 'Draft'
  | 'InProgress'
  | 'ComputedReady'
  | 'PendingPayment'
  | 'Paid'
  | 'UnderCaReview'
  | 'ReadyToFile'
  | 'Filed'
  | 'Processed'
  | 'Failed';

export type Regime = 'Old' | 'New';

export type Gateway = 'Razorpay' | 'Cashfree' | 'Wallet';

export type PaymentStatus = 'Created' | 'Pending' | 'Paid' | 'Failed' | 'Refunded';

export type DocumentKind =
  | 'Form16'
  | 'AIS'
  | 'TIS'
  | 'Form26AS'
  | 'BankStatement'
  | 'CapitalGainStmt'
  | 'GstData'
  | 'Other';

export type DocumentStatus =
  | 'Uploaded'
  | 'Scanning'
  | 'Extracting'
  | 'Extracted'
  | 'NeedsReview'
  | 'Verified'
  | 'Failed';

export type AssignmentStatus = 'Unassigned' | 'Assigned' | 'InReview' | 'Completed';

export type ReviewOutcome = 'Approved' | 'ChangesRequested';

export type IncomeType =
  | 'Salary'
  | 'HouseProperty'
  | 'CapitalGains'
  | 'Business'
  | 'OtherSources';

export type CapitalGainTerm = 'Short' | 'Long';

export type TicketStatus = 'Open' | 'Pending' | 'Resolved' | 'Closed';

export type NotificationChannel = 'Email' | 'Sms' | 'WhatsApp' | 'InApp';

export type LeadStage = 'New' | 'Contacted' | 'Qualified' | 'Converted' | 'Lost';

// ---- Generic envelope helpers --------------------------------------------
/** List endpoints return PagedResult<T>. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

/** RFC 7807 problem+json shape (error responses). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Stable namespaced code, e.g. "VALIDATION.FAILED", "AUTH.OTP_INVALID". */
  code?: string;
  correlationId?: string;
  errors?: ProblemFieldError[];
}

export interface ProblemFieldError {
  field: string;
  code: string;
  message: string;
  rejectedValue?: string;
}

// ---- Auth ----------------------------------------------------------------
export type Role =
  | 'User'
  | 'CA'
  | 'CaFirmAdmin'
  | 'Reviewer'
  | 'Ops'
  | 'Admin'
  | 'SuperAdmin'
  | 'Affiliate';

export interface User {
  id: Guid;
  fullName: string;
  email: string;
  mobile: string;
  roles: Role[];
  panMasked?: string | null;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  mobile: string;
}

export interface RegisterResponse {
  userId: Guid;
}

export type OtpPurpose = 'login' | 'register' | 'reset' | 'add-channel';

export interface OtpRequestBody {
  identifier: string;
  purpose: OtpPurpose;
}

export interface OtpRequestResponse {
  otpToken: string;
  expiresInSeconds: number;
  /** Returned only in Development so login works without a real SMS gateway. */
  devOtp?: string | null;
}

export interface OtpVerifyBody {
  otpToken: string;
  code: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface OtpVerifyResponse extends AuthTokens {
  user: User;
}

export interface RefreshRequest {
  refreshToken: string;
}

export type RefreshResponse = AuthTokens;

export interface LogoutRequest {
  refreshToken: string;
}

// ---- Tax core ------------------------------------------------------------
export interface AssessmentYearDto {
  id: Guid;
  code: string; // "AY2025-26"
  startDate: IsoDateTime;
  endDate: IsoDateTime;
  isActive: boolean;
}

export interface TaxReturnDto {
  id: Guid;
  userId: Guid;
  assessmentYearId: Guid;
  assessmentYearCode?: string;
  itrType: ItrType;
  status: ReturnStatus;
  regime: Regime;
  ruleSetVersion?: string | null;
  questionnaireSchemaVersion?: string | null;
  createdAt: IsoDateTime;
  submittedAt?: IsoDateTime | null;
  acknowledgmentNumber?: string | null;
}

export interface CreateReturnRequest {
  assessmentYearCode: string; // e.g. "AY2025-26"
  itrType?: ItrType;
}

export interface IncomeSourceDto {
  id: Guid;
  taxReturnId: Guid;
  type: IncomeType;
  label: string;
  amount: DecimalString;
}

export interface SalaryDetailDto {
  id: Guid;
  taxReturnId: Guid;
  employer: string;
  gross: DecimalString;
  hra: DecimalString;
  stdDeduction: DecimalString;
}

export interface HousePropertyDto {
  id: Guid;
  taxReturnId: Guid;
  type: string;
  annualValue: DecimalString;
  interestOnLoan: DecimalString;
}

export interface CapitalGainDto {
  id: Guid;
  taxReturnId: Guid;
  assetType: string;
  term: CapitalGainTerm;
  salePrice: DecimalString;
  costOfAcquisition: DecimalString;
  gain: DecimalString;
}

export interface BusinessIncomeDto {
  id: Guid;
  taxReturnId: Guid;
  isPresumptive: boolean;
  turnover: DecimalString;
  presumptiveSection?: string | null;
  speculativeFlag: boolean;
}

export interface DeductionDto {
  id: Guid;
  taxReturnId: Guid;
  section: string; // "80C"
  amount: DecimalString;
  description?: string | null;
}

/** One line in the engine's explainability trace. */
export interface ComputationTraceLine {
  label: string;
  amount: DecimalString;
  note?: string;
}

export interface ComputationDto {
  id: Guid;
  taxReturnId: Guid;
  regime: Regime;
  grossTotalIncome: DecimalString;
  totalDeductions: DecimalString;
  taxableIncome: DecimalString;
  taxBeforeCess: DecimalString;
  cess: DecimalString;
  rebate87A: DecimalString;
  surcharge: DecimalString;
  totalTax: DecimalString;
  tdsPaid: DecimalString;
  advanceTax: DecimalString;
  /** Positive = refund due, negative = payable. */
  refundOrPayable: DecimalString;
  computedAt: IsoDateTime;
  trace?: ComputationTraceLine[];
}

/** Old-vs-new side-by-side response. */
export interface RegimeComparisonDto {
  old: ComputationDto;
  new: ComputationDto;
  recommended: Regime;
  savings: DecimalString;
}

// ---- Documents -----------------------------------------------------------
export interface DocumentDto {
  id: Guid;
  taxReturnId?: Guid | null;
  kind: DocumentKind;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: DocumentStatus;
  createdAt: IsoDateTime;
}

/** Two-step pre-signed upload (Decision Log D-2). */
export interface InitiateUploadRequest {
  fileName: string;
  contentType: string;
  sizeBytes: number;
  kind: DocumentKind;
  taxReturnId?: Guid | null;
}

export interface InitiateUploadResponse {
  documentId: Guid;
  /** Pre-signed URL the client PUTs the file bytes to. */
  uploadUrl: string;
  /** Headers the client must echo on the PUT. */
  requiredHeaders?: Record<string, string>;
}

export interface ExtractionFieldDto {
  name: string;
  value: string;
  confidence: number; // 0..1
}

export interface DocumentExtractionDto {
  id: Guid;
  documentId: Guid;
  status: DocumentStatus;
  confidenceScore: number;
  fields: ExtractionFieldDto[];
  reviewedAt?: IsoDateTime | null;
}

// ---- Payments ------------------------------------------------------------
export interface PlanDto {
  id: Guid;
  code: string;
  name: string;
  price: DecimalString;
  features: string[];
}

export interface PaymentDto {
  id: Guid;
  taxReturnId?: Guid | null;
  amount: DecimalString;
  currency: string;
  gateway: Gateway;
  gatewayOrderId: string;
  gatewayPaymentId?: string | null;
  status: PaymentStatus;
  createdAt: IsoDateTime;
}

export interface CreateOrderRequest {
  planCode: string;
  taxReturnId?: Guid | null;
  gateway: Gateway;
  couponCode?: string | null;
}

export interface CreateOrderResponse {
  paymentId: Guid;
  gatewayOrderId: string;
  amount: DecimalString;
  currency: string;
  /** Public key/token the gateway widget needs (mock in demo). */
  gatewayKey?: string;
}

export interface VerifyPaymentRequest {
  gatewayOrderId: string;
  gatewayPaymentId: string;
  signature: string;
}

export interface InvoiceDto {
  id: Guid;
  paymentId: Guid;
  number: string;
  amount: DecimalString;
  gst: DecimalString;
  issuedAt: IsoDateTime;
}

export interface WalletDto {
  id: Guid;
  balance: DecimalString;
}

export interface CouponValidateRequest {
  code: string;
  planCode: string;
}

export interface CouponValidateResponse {
  valid: boolean;
  discount: DecimalString;
  finalPrice: DecimalString;
  message?: string;
}

// ---- CA workflow ---------------------------------------------------------
export interface CaAssignmentDto {
  id: Guid;
  taxReturnId: Guid;
  caUserId: Guid;
  status: AssignmentStatus;
  assignedAt: IsoDateTime;
  completedAt?: IsoDateTime | null;
}

export interface ReviewDto {
  id: Guid;
  caAssignmentId: Guid;
  outcome: ReviewOutcome;
  comments: string;
  createdAt: IsoDateTime;
}

// ---- Notices / Tickets / Notifications -----------------------------------
export interface NoticeDto {
  id: Guid;
  taxReturnId?: Guid | null;
  noticeType: string;
  section: string;
  receivedAt: IsoDateTime;
  dueDate?: IsoDateTime | null;
  status: string;
}

export interface TicketDto {
  id: Guid;
  subject: string;
  status: TicketStatus;
  priority: string;
  createdAt: IsoDateTime;
}

export interface TicketMessageDto {
  id: Guid;
  ticketId: Guid;
  senderUserId: Guid;
  body: string;
  createdAt: IsoDateTime;
}

export interface NotificationDto {
  id: Guid;
  channel: NotificationChannel;
  template: string;
  status: string;
  sentAt?: IsoDateTime | null;
}

// ---- Admin / CRM ---------------------------------------------------------
export interface LeadDto {
  id: Guid;
  name: string;
  email: string;
  mobile: string;
  source: string;
  stage: LeadStage;
  createdAt: IsoDateTime;
}

export interface AuditLogDto {
  id: Guid;
  actorUserId?: Guid | null;
  action: string;
  entityType: string;
  entityId: string;
  createdAt: IsoDateTime;
}
