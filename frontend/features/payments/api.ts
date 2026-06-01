// ---------------------------------------------------------------------------
// features/payments/api.ts
// TanStack Query keys + thin fetchers for the Payments / Wallet / Coupons module.
// All calls go through lib/api (bearer + refresh-on-401 + RFC7807 → ApiError).
//
// Backend routes (docs 04 §4.2; ":verb" sub-resource convention):
//   GET  /pricing/plans
//   POST /payments/orders            (Idempotency-Key header)
//   POST /payments/{id}:verify
//   GET  /payments                   (paged)
//   GET  /payments/{id}
//   GET  /payments/{id}/invoice
//   GET  /wallet
//   GET  /wallet/transactions        (paged)
//   POST /coupons:validate
//   POST /coupons:apply
// ---------------------------------------------------------------------------

import { api, apiGet, apiPost } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  CouponApplyBody,
  CouponResultDto,
  CouponValidateBody,
  CreateOrderBody,
  CreateOrderResponse,
  InvoiceDto,
  PaymentDto,
  PlanDto,
  VerifyPaymentBody,
  VerifyPaymentResponse,
  WalletDto,
  WalletTransactionDto,
} from './types';

/** Idempotency header for money-moving POSTs (docs 04 §4 "Idempotency"). */
const IDEMPOTENCY_HEADER = 'Idempotency-Key';

/** Generate a client idempotency token (UUID; falls back for old runtimes). */
export function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `idemp-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/** Centralised query keys for payments / wallet / pricing. */
export const paymentsKeys = {
  all: ['payments'] as const,
  lists: () => [...paymentsKeys.all, 'list'] as const,
  list: (page: number, pageSize: number) =>
    [...paymentsKeys.lists(), { page, pageSize }] as const,
  detail: (id: string) => [...paymentsKeys.all, 'detail', id] as const,
  invoice: (id: string) => [...paymentsKeys.all, 'invoice', id] as const,
  plans: ['pricing', 'plans'] as const,
  wallet: ['wallet'] as const,
  walletTxns: (page: number, pageSize: number) =>
    ['wallet', 'transactions', { page, pageSize }] as const,
};

// ----------------------------------------------------------------- pricing

/** GET /pricing/plans — active filing-fee plans/SKUs. */
export function listPlans(): Promise<PlanDto[]> {
  return apiGet<PlanDto[]>('/pricing/plans');
}

// ----------------------------------------------------------------- payments

/** GET /payments — the caller's payments, newest first. */
export function listPayments(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<PaymentDto>> {
  return apiGet<PagedResult<PaymentDto>>('/payments', { params: { page, pageSize } });
}

/** GET /payments/{id} — single payment detail. */
export function getPayment(id: string): Promise<PaymentDto> {
  return apiGet<PaymentDto>(`/payments/${id}`);
}

/** GET /payments/{id}/invoice — GST invoice projection (JSON) for a captured payment. */
export function getInvoice(paymentId: string): Promise<InvoiceDto> {
  return apiGet<InvoiceDto>(`/payments/${paymentId}/invoice`);
}

/**
 * GET /payments/{id}/invoice:pdf — the rendered GST tax-invoice PDF (Reporting
 * module, docs 09 §9.2). Returns the blob + a best-effort filename so the table
 * can trigger a browser download. Goes through the API so the bearer + RBAC apply.
 */
export async function downloadInvoicePdf(
  paymentId: string,
): Promise<{ blob: Blob; fileName: string | null }> {
  const res = await api.get(`/payments/${paymentId}/invoice:pdf`, { responseType: 'blob' });
  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition ? /filename="?([^";]+)"?/i.exec(disposition) : null;
  return { blob: res.data as Blob, fileName: match?.[1]?.trim() ?? null };
}

/**
 * POST /payments/orders — create a payment order (idempotent via header).
 * Pass an explicit key so retries of the SAME logical order don't double-charge.
 */
export function createOrder(
  body: CreateOrderBody,
  idempotencyKey: string,
): Promise<CreateOrderResponse> {
  return apiPost<CreateOrderResponse>('/payments/orders', body, {
    headers: { [IDEMPOTENCY_HEADER]: idempotencyKey },
  });
}

/** POST /payments/{id}:verify — verify the gateway signature & capture. */
export function verifyPayment(
  id: string,
  body: VerifyPaymentBody,
): Promise<VerifyPaymentResponse> {
  return apiPost<VerifyPaymentResponse>(`/payments/${id}:verify`, body);
}

// ----------------------------------------------------------------- wallet

/** GET /wallet — current user's wallet balance. */
export function getWallet(): Promise<WalletDto> {
  return apiGet<WalletDto>('/wallet');
}

/** GET /wallet/transactions — wallet ledger, newest first. */
export function listWalletTransactions(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<WalletTransactionDto>> {
  return apiGet<PagedResult<WalletTransactionDto>>('/wallet/transactions', {
    params: { page, pageSize },
  });
}

// ----------------------------------------------------------------- coupons

/** POST /coupons:validate — check a coupon against a plan's price. */
export function validateCoupon(body: CouponValidateBody): Promise<CouponResultDto> {
  return apiPost<CouponResultDto>('/coupons:validate', body);
}

/** POST /coupons:apply — apply a coupon to an existing unpaid order. */
export function applyCoupon(body: CouponApplyBody): Promise<CouponResultDto> {
  return apiPost<CouponResultDto>('/coupons:apply', body);
}
