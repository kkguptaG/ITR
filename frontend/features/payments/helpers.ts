// ---------------------------------------------------------------------------
// features/payments/helpers.ts
// Pure presentational + mock-gateway helpers for the Payments area.
//   • Status label/tone maps for payments & wallet ledger entries.
//   • A GST line-item breakdown formatter for the checkout summary.
//   • mockGatewaySignature(): STUB that stands in for the Razorpay/Cashfree
//     checkout widget. A real PSP's JS returns the signature to the client; the
//     no-PSP demo recomputes the SAME HMAC-SHA256("orderId|paymentId") the
//     RazorpayStub verifies server-side, using the dev key secret. This keeps
//     the verify path doing REAL crypto without a payment account.
// ---------------------------------------------------------------------------

import type { PaymentStatus } from './types';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

/** Human label for a payment status. */
const PAYMENT_STATUS_LABELS: Record<PaymentStatus, string> = {
  Created: 'Created',
  Pending: 'Pending',
  Paid: 'Paid',
  Failed: 'Failed',
  Refunded: 'Refunded',
};

export function formatPaymentStatus(status: PaymentStatus | string): string {
  return PAYMENT_STATUS_LABELS[status as PaymentStatus] ?? status;
}

/** Badge tone for a payment status. */
const PAYMENT_STATUS_TONES: Record<PaymentStatus, Tone> = {
  Created: 'neutral',
  Pending: 'warning',
  Paid: 'success',
  Failed: 'danger',
  Refunded: 'info',
};

export function paymentStatusTone(status: PaymentStatus | string): Tone {
  return PAYMENT_STATUS_TONES[status as PaymentStatus] ?? 'neutral';
}

/** Friendly gateway label ("razorpay" → "Razorpay"). */
export function formatGateway(gateway: string): string {
  if (!gateway) return '—';
  return gateway.charAt(0).toUpperCase() + gateway.slice(1).toLowerCase();
}

/** Tone for a wallet ledger entry: credits are positive (success), debits neutral. */
export function walletTxnTone(type: string): Tone {
  return /credit|refund|reward|bonus/i.test(type) ? 'success' : 'neutral';
}

/** Whether a wallet ledger entry adds to the balance (for +/- sign display). */
export function isWalletCredit(type: string): boolean {
  return /credit|refund|reward|bonus/i.test(type);
}

// --------------------------------------------------------------- mock gateway

const DEV_KEY_SECRET =
  process.env.NEXT_PUBLIC_PAYMENTS_DEV_SECRET ?? 'razorpay_dev_secret_change_me';

/** A plausible gateway payment id (mirrors Razorpay's "pay_..." format). */
export function mockGatewayPaymentId(): string {
  const rand =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID().replace(/-/g, '')
      : Math.random().toString(16).slice(2);
  return `pay_${rand.slice(0, 14)}`;
}

/**
 * STUB: compute the gateway signature the way the PSP's checkout widget would.
 * Real flow: Razorpay JS returns razorpay_signature = HMAC_SHA256(order|payment,
 * key_secret). Here we recompute it with the dev secret so the server's verify
 * (real HMAC + fixed-time compare) succeeds without a live gateway.
 */
export async function mockGatewaySignature(
  orderId: string,
  paymentId: string,
): Promise<string> {
  const subtle = globalThis.crypto?.subtle;
  if (!subtle) {
    throw new Error('Web Crypto unavailable; cannot simulate the gateway signature.');
  }
  const enc = new TextEncoder();
  const key = await subtle.importKey(
    'raw',
    enc.encode(DEV_KEY_SECRET),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  );
  const mac = await subtle.sign('HMAC', key, enc.encode(`${orderId}|${paymentId}`));
  return toHex(new Uint8Array(mac));
}

function toHex(bytes: Uint8Array): string {
  let out = '';
  for (const b of bytes) out += b.toString(16).padStart(2, '0');
  return out;
}
