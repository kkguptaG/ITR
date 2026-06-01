'use client';

// ---------------------------------------------------------------------------
// CheckoutDialog — the reusable filing-fee checkout, used by BOTH the Payments
// page and the filing wizard's payment step.
//
// Flow (against the mock gateway, Decision Log: AWS/SQS + Razorpay stub):
//   1. POST /payments/orders {returnId, planCode, couponCode?, gateway, useWallet}
//      (with an Idempotency-Key) → priced breakdown + gatewayOrderId.
//   2. If the wallet covered everything (requiresGatewayCheckout=false) the order
//      is already Paid → show success.
//   3. Otherwise simulate the gateway widget: mint a mock paymentId + signature
//      (STUB for the PSP's checkout JS), then POST /payments/{id}:verify.
//   4. On success the linked return advances to Paid; we surface the invoice no.
//
// The order is created once the dialog opens (or when the gateway/coupon
// changes) so the user always sees the live amount before paying.
// ---------------------------------------------------------------------------

import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslations } from 'next-intl';
import { ShieldCheck, Wallet, CheckCircle2, Lock } from 'lucide-react';
import { Modal, Button, Alert, Spinner, Select } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatInr } from '@/lib/format';
import {
  createOrder,
  getWallet,
  newIdempotencyKey,
  paymentsKeys,
  verifyPayment,
} from '../api';
import {
  formatGateway,
  mockGatewayPaymentId,
  mockGatewaySignature,
} from '../helpers';
import { CouponField } from './CouponField';
import type { CreateOrderResponse, GatewayCode, VerifyPaymentResponse } from '../types';

export interface CheckoutDialogProps {
  open: boolean;
  onClose: () => void;
  /** The return whose filing fee is being paid. */
  returnId: string;
  /** The plan/SKU to price the fee from. */
  planCode: string;
  /** Optional human plan name shown in the header. */
  planName?: string;
  /** Fired after a successful capture (e.g. advance the wizard). */
  onPaid?: (result: VerifyPaymentResponse) => void;
}

type Phase = 'pricing' | 'idle' | 'paying' | 'paid' | 'error';

const GATEWAY_OPTIONS: { value: GatewayCode; label: string }[] = [
  { value: 'razorpay', label: 'Razorpay' },
  { value: 'cashfree', label: 'Cashfree' },
];

export function CheckoutDialog({
  open,
  onClose,
  returnId,
  planCode,
  planName,
  onPaid,
}: CheckoutDialogProps) {
  const t = useTranslations('payments');
  const tc = useTranslations('common');
  const queryClient = useQueryClient();

  const [gateway, setGateway] = useState<GatewayCode>('razorpay');
  const [useWallet, setUseWallet] = useState(false);
  const [couponCode, setCouponCode] = useState<string | null>(null);
  const [order, setOrder] = useState<CreateOrderResponse | null>(null);
  const [phase, setPhase] = useState<Phase>('pricing');
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<VerifyPaymentResponse | null>(null);

  // One idempotency key per dialog session so re-pricing/retries don't double-charge.
  const idempotencyKey = useRef<string>(newIdempotencyKey());

  const walletQuery = useQuery({
    queryKey: paymentsKeys.wallet,
    queryFn: getWallet,
    enabled: open,
  });

  // (Re)create the order whenever inputs change while the dialog is open.
  const priceOrder = useCallback(async () => {
    setPhase('pricing');
    setError(null);
    try {
      const created = await createOrder(
        {
          returnId,
          planCode,
          couponCode: couponCode ?? undefined,
          gateway,
          useWallet,
        },
        idempotencyKey.current,
      );
      setOrder(created);
      // A wallet-only order is captured instantly by the server.
      if (created.status === 'Paid' && !created.requiresGatewayCheckout) {
        setResult({
          paymentId: created.paymentId,
          status: created.status,
          invoiceId: null,
          invoiceNumber: null,
          taxReturnId: returnId,
          returnStatus: null,
        });
        setPhase('paid');
        void queryClient.invalidateQueries({ queryKey: paymentsKeys.lists() });
        void queryClient.invalidateQueries({ queryKey: paymentsKeys.wallet });
      } else {
        setPhase('idle');
      }
    } catch (err) {
      setError(
        err instanceof ApiError
          ? (err.problem.detail ?? err.message)
          : 'Could not price this order. Please try again.',
      );
      setPhase('error');
    }
  }, [returnId, planCode, couponCode, gateway, useWallet, queryClient]);

  // Price the order when the dialog opens and re-price when the inputs change.
  // Each distinct (gateway, coupon, wallet) combination is a fresh order intent,
  // so we mint a new Idempotency-Key per re-price; retries of the SAME intent
  // (e.g. a transient network error) reuse it because priceOrder's identity is
  // stable for fixed inputs.
  useEffect(() => {
    if (!open) return;
    idempotencyKey.current = newIdempotencyKey();
    setResult(null);
    void priceOrder();
  }, [open, priceOrder]);

  const payMutation = useMutation({
    mutationFn: async (): Promise<VerifyPaymentResponse> => {
      if (!order?.gatewayOrderId) {
        throw new Error('No gateway order to pay.');
      }
      // STUB: stand in for the PSP checkout widget returning a signed result.
      const gatewayPaymentId = mockGatewayPaymentId();
      const signature = await mockGatewaySignature(order.gatewayOrderId, gatewayPaymentId);
      return verifyPayment(order.paymentId, { gatewayPaymentId, signature });
    },
    onMutate: () => {
      setError(null);
      setPhase('paying');
    },
    onSuccess: (res) => {
      setResult(res);
      setPhase('paid');
      void queryClient.invalidateQueries({ queryKey: paymentsKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: paymentsKeys.wallet });
      onPaid?.(res);
    },
    onError: (err) => {
      setError(
        err instanceof ApiError
          ? (err.problem.detail ?? err.message)
          : 'Payment could not be completed. Please try again.',
      );
      setPhase('error');
    },
  });

  const balance = walletQuery.data?.balance ?? 0;
  const busy = phase === 'pricing' || phase === 'paying';

  return (
    <Modal
      open={open}
      onClose={busy ? () => undefined : onClose}
      title={t('payFee')}
      description={planName ? `${t('plan')}: ${planName}` : undefined}
      size="md"
      footer={
        phase === 'paid' ? (
          <Button onClick={onClose}>{tc('close')}</Button>
        ) : (
          <>
            <Button variant="ghost" onClick={onClose} disabled={busy}>
              {tc('cancel')}
            </Button>
            <Button
              onClick={() => payMutation.mutate()}
              loading={phase === 'paying'}
              disabled={phase !== 'idle' || !order || !order.requiresGatewayCheckout}
            >
              {order ? t('payWith', { gateway: formatGateway(gateway) }) : tc('continue')}
            </Button>
          </>
        )
      }
    >
      {phase === 'paid' && result ? (
        <div className="space-y-3 py-2 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-money-50 text-money-600">
            <CheckCircle2 className="h-6 w-6" aria-hidden="true" />
          </div>
          <h3 className="text-base font-semibold text-ink-900">Payment successful</h3>
          <p className="text-sm text-ink-500">
            Your filing fee is paid and your return is ready to file.
            {result.invoiceNumber ? (
              <>
                {' '}
                Invoice <span className="font-medium text-ink-700">{result.invoiceNumber}</span>.
              </>
            ) : null}
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {/* Coupon */}
          <CouponField
            planCode={planCode}
            onApplied={(code) => {
              setCouponCode(code);
            }}
            onCleared={() => setCouponCode(null)}
          />

          {/* Wallet toggle */}
          {balance > 0 && (
            <label className="flex cursor-pointer items-center justify-between rounded-xl border border-ink-200 px-3.5 py-3">
              <span className="flex items-center gap-2 text-sm text-ink-800">
                <Wallet className="h-4 w-4 text-ink-500" aria-hidden="true" />
                {t('walletCredit')} ({formatInr(balance)})
              </span>
              <input
                type="checkbox"
                checked={useWallet}
                onChange={(e) => setUseWallet(e.target.checked)}
                className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500"
              />
            </label>
          )}

          {/* Gateway */}
          <div className="space-y-1.5">
            <label htmlFor="checkout-gateway" className="text-sm font-medium text-ink-700">
              Pay using
            </label>
            <Select
              id="checkout-gateway"
              value={gateway}
              onChange={(e) => setGateway(e.target.value as GatewayCode)}
              options={GATEWAY_OPTIONS}
              disabled={busy}
            />
          </div>

          {/* Breakdown */}
          {phase === 'pricing' ? (
            <div className="flex justify-center py-6">
              <Spinner label="Pricing…" />
            </div>
          ) : order ? (
            <PriceBreakdown order={order} />
          ) : null}

          {error && <Alert variant="error">{error}</Alert>}

          <div className="flex items-center justify-center gap-4 pt-1 text-xs text-ink-400">
            <span className="inline-flex items-center gap-1">
              <Lock className="h-3.5 w-3.5" aria-hidden="true" />
              {t('noCardStored')}
            </span>
            <span className="inline-flex items-center gap-1">
              <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />
              {t('inclGst')}
            </span>
          </div>
        </div>
      )}
    </Modal>
  );
}

// ----------------------------------------------------------------- breakdown

function PriceBreakdown({ order }: { order: CreateOrderResponse }) {
  return (
    <dl className="space-y-1.5 rounded-xl bg-ink-50 p-3.5 text-sm">
      <Row label="Filing fee" value={formatInr(order.baseAmount, { paise: true })} />
      {order.discountAmount > 0 && (
        <Row
          label="Coupon discount"
          value={`− ${formatInr(order.discountAmount, { paise: true })}`}
          tone="money"
        />
      )}
      {order.walletApplied > 0 && (
        <Row
          label="Wallet applied"
          value={`− ${formatInr(order.walletApplied, { paise: true })}`}
          tone="money"
        />
      )}
      <Row label="GST (18%)" value={formatInr(order.gstAmount, { paise: true })} />
      <div className="mt-1 border-t border-ink-200 pt-2">
        <Row
          label="Amount payable"
          value={formatInr(order.amountPayable, { paise: true })}
          strong
        />
      </div>
    </dl>
  );
}

function Row({
  label,
  value,
  strong,
  tone,
}: {
  label: string;
  value: string;
  strong?: boolean;
  tone?: 'money';
}) {
  return (
    <div className="flex items-center justify-between">
      <dt className={strong ? 'font-semibold text-ink-900' : 'text-ink-600'}>{label}</dt>
      <dd
        className={
          strong
            ? 'font-semibold text-ink-900'
            : tone === 'money'
              ? 'font-medium text-money-700'
              : 'text-ink-800'
        }
      >
        {value}
      </dd>
    </div>
  );
}
