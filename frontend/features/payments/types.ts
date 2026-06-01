// ---------------------------------------------------------------------------
// features/payments/types.ts
// Wire types for the Payments / Wallet / Coupons module, mirroring the backend
// DTOs EXACTLY (ASP.NET Core, camelCase wire, `decimal` serialized as a JSON
// number — NOT a string, since the API uses JsonSerializerDefaults.Web).
//
// Feature-local because the real contract in
// backend/src/TallyG.Tax.Api/Modules/Payments/PaymentDtos.cs is richer than the
// generic placeholders in lib/api-types.ts (resolved pricing breakdown on the
// order, gateway is a lowercase string, coupon result shape, etc.).
//
// Source of truth: backend Modules/Payments/PaymentDtos.cs + docs 04 §4.2.
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime } from '@/lib/api-types';

/** Gateway identifiers as the backend emits/accepts them (lowercase). */
export type GatewayCode = 'razorpay' | 'cashfree' | 'wallet';

// ----------------------------------------------------------------- pricing/plans

/** A purchasable plan/SKU surfaced to the client checkout (GET /pricing/plans). */
export interface PlanDto {
  id: Guid;
  code: string;
  name: string;
  price: number;
  billingPeriod: string;
  features: string[];
  isActive: boolean;
}

// ----------------------------------------------------------------- create order

/**
 * POST /payments/orders body. The order is for the filing fee of `returnId`,
 * priced from `planCode`, with an optional `couponCode` discount and an optional
 * wallet draw-down. `gateway` defaults to "razorpay"; "wallet" pays instantly.
 * Send an Idempotency-Key header to make retries safe (the client sets one).
 */
export interface CreateOrderBody {
  returnId: Guid;
  planCode: string;
  couponCode?: string | null;
  gateway?: GatewayCode | null;
  useWallet?: boolean;
}

/**
 * POST /payments/orders response. Mirrors a gateway "create order" result plus
 * the resolved pricing so the (mock) checkout can render the breakdown.
 */
export interface CreateOrderResponse {
  paymentId: Guid;
  gateway: string;
  gatewayOrderId: string | null;
  gatewayKeyId: string | null;
  currency: string;
  baseAmount: number;
  discountAmount: number;
  walletApplied: number;
  gstAmount: number;
  amountPayable: number;
  status: string;
  /** false when the wallet covered the whole amount → no checkout widget needed. */
  requiresGatewayCheckout: boolean;
}

// ----------------------------------------------------------------- verify

/** POST /payments/{id}:verify body — the signed result returned by the gateway. */
export interface VerifyPaymentBody {
  gatewayPaymentId: string;
  signature: string;
}

/** POST /payments/{id}:verify response. */
export interface VerifyPaymentResponse {
  paymentId: Guid;
  status: string;
  invoiceId: Guid | null;
  invoiceNumber: string | null;
  taxReturnId: Guid | null;
  returnStatus: string | null;
}

// ----------------------------------------------------------------- payment views

/** Payment status (PaymentStatus enum). */
export type PaymentStatus = 'Created' | 'Pending' | 'Paid' | 'Failed' | 'Refunded';

/** List/detail projection of a payment (GET /payments, GET /payments/{id}). */
export interface PaymentDto {
  id: Guid;
  taxReturnId: Guid | null;
  gateway: string;
  gatewayOrderId: string | null;
  gatewayPaymentId: string | null;
  amount: number;
  currency: string;
  gst: number;
  discountAmount: number;
  walletApplied: number;
  status: PaymentStatus;
  invoiceId: Guid | null;
  invoiceNumber: string | null;
  createdAt: IsoDateTime;
}

// ----------------------------------------------------------------- invoice

/** GST invoice projection (GET /payments/{id}/invoice). */
export interface InvoiceDto {
  id: Guid;
  paymentId: Guid;
  number: string;
  amount: number;
  gst: number;
  total: number;
  gstinSeller: string | null;
  placeOfSupply: string | null;
  issuedAt: IsoDateTime;
}

// ----------------------------------------------------------------- wallet

/** GET /wallet response. */
export interface WalletDto {
  id: Guid;
  balance: number;
  currency: string;
  updatedAt: IsoDateTime;
}

/** A single ledger entry (GET /wallet/transactions). */
export interface WalletTransactionDto {
  id: Guid;
  type: string;
  amount: number;
  balanceAfter: number;
  reference: string | null;
  note: string | null;
  createdAt: IsoDateTime;
}

// ----------------------------------------------------------------- coupons

/** POST /coupons:validate body — check a coupon against a plan's price. */
export interface CouponValidateBody {
  code: string;
  planCode: string;
}

/** POST /coupons:apply body — apply a coupon to an existing (unpaid) order. */
export interface CouponApplyBody {
  code: string;
  paymentId: Guid;
}

/** Coupon pricing result shared by :validate and :apply. */
export interface CouponResultDto {
  code: string;
  valid: boolean;
  type: string;
  value: number;
  baseAmount: number;
  discountAmount: number;
  netAmount: number;
  message: string | null;
}
