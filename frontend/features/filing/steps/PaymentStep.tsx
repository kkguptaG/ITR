'use client';

// ---------------------------------------------------------------------------
// Step 7 — Payment. Choose a filing-fee plan, optionally apply a coupon, create
// an order (POST /payments/orders), then (mock) verify the gateway signature
// (POST /payments/{id}:verify) which captures the payment, issues the invoice
// and marks the return Paid. Razorpay is stubbed end-to-end, so "Pay now"
// simulates the gateway callback with a mock paymentId + signature.
// ---------------------------------------------------------------------------

import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ShieldCheck, Tag, X } from 'lucide-react';
import { Alert, Button, Card, Field, Input } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatInr } from '@/lib/format';
import {
  createOrder,
  filingKeys,
  getPlans,
  validateCoupon,
  verifyPayment,
} from '../api';
import type { CouponResultDto, CreateOrderResponse } from '../types';
import { couponSchema, type CouponFormValues } from '../schemas';
import { useWizard } from '../WizardContext';
import { useInvalidateReturn } from '../useReturn';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { PlanPicker } from '../components/PlanPicker';
import { mockGatewayPaymentId, mockGatewaySignature } from '@/features/payments/helpers';

export function PaymentStep() {
  const t = useTranslations('wizard');
  const tp = useTranslations('payments');
  const tc = useTranslations('common');
  const { returnId, detail, goNext } = useWizard();
  const invalidate = useInvalidateReturn(returnId);

  const alreadyPaid = ['Paid', 'ReadyToFile', 'UnderCaReview', 'Filed', 'Processed'].includes(detail.status);

  const plansQuery = useQuery({ queryKey: filingKeys.plans, queryFn: getPlans, staleTime: 60 * 60_000 });

  const [selectedCode, setSelectedCode] = useState<string | null>(null);
  const [coupon, setCoupon] = useState<CouponResultDto | null>(null);
  const [order, setOrder] = useState<CreateOrderResponse | null>(null);

  // Default to the first plan once loaded.
  const plans = plansQuery.data;
  const effectiveCode = selectedCode ?? plans?.[0]?.code ?? null;
  const selectedPlan = plans?.find((p) => p.code === effectiveCode);

  // ---- coupon
  const couponForm = useForm<CouponFormValues>({
    resolver: zodResolver(couponSchema),
    defaultValues: { code: '' },
  });
  const couponMutation = useMutation({
    mutationFn: (code: string) => validateCoupon({ code, planCode: effectiveCode as string }),
    onSuccess: (res) => {
      if (res.valid) setCoupon(res);
      else couponForm.setError('code', { message: res.message ?? t('couponInvalid') });
    },
  });

  // ---- order + verify (the mock gateway round-trip)
  const payMutation = useMutation({
    mutationFn: async () => {
      const created = await createOrder({
        returnId,
        planCode: effectiveCode as string,
        couponCode: coupon?.code ?? null,
        gateway: 'razorpay',
      });
      setOrder(created);

      // If the order is already settled (e.g. ₹0 after coupon, or wallet), skip verify.
      if (!created.requiresGatewayCheckout || created.status === 'Paid') {
        return { status: created.status };
      }

      // STUB: simulate the Razorpay checkout callback. The server verifies a real
      // HMAC-SHA256("orderId|paymentId") with the dev key, so we must compute the
      // matching signature here (a literal placeholder is rejected) — same scheme
      // as CheckoutDialog.
      const gatewayPaymentId = mockGatewayPaymentId();
      const signature = await mockGatewaySignature(created.gatewayOrderId ?? '', gatewayPaymentId);
      const verified = await verifyPayment(created.paymentId, {
        gatewayPaymentId,
        signature,
      });
      return { status: verified.status };
    },
    onSuccess: () => {
      invalidate();
      goNext();
    },
  });

  const price = useMemo(() => {
    const base = selectedPlan?.price ?? 0;
    const discount = coupon?.discountAmount ?? 0;
    const net = coupon?.netAmount ?? Math.max(base - discount, 0);
    const gst = Math.round(net * 0.18 * 100) / 100;
    return { base, discount, net, gst, total: net + gst };
  }, [selectedPlan, coupon]);

  const payError =
    payMutation.error instanceof ApiError
      ? (payMutation.error.problem.detail ?? payMutation.error.message)
      : payMutation.error
        ? tc('retry')
        : null;

  if (alreadyPaid) {
    return (
      <>
        <WizardStep title={t('paymentTitle')} description={t('paymentSubtitle')}>
          <Alert variant="success" title={tp('alreadyPaidTitle')}>
            {tp('alreadyPaidBody')}
          </Alert>
        </WizardStep>
        <WizardFooter
          primary={
            <Button type="button" onClick={goNext}>
              {tc('continue')}
            </Button>
          }
        />
      </>
    );
  }

  return (
    <>
      <WizardStep title={t('paymentTitle')} description={t('paymentSubtitle')}>
        <PlanPicker
          plans={plans}
          selectedCode={effectiveCode}
          onSelect={(code) => {
            setSelectedCode(code);
            setCoupon(null); // a coupon is plan-specific; re-validate after a plan change
          }}
          loading={plansQuery.isLoading}
        />

        {/* Coupon */}
        <Card className="p-4">
          {coupon ? (
            <div className="flex items-center justify-between gap-2">
              <span className="inline-flex items-center gap-2 text-sm text-money-700">
                <Tag className="h-4 w-4" aria-hidden="true" />
                {t('couponApplied', { code: coupon.code, amount: formatInr(coupon.discountAmount) })}
              </span>
              <Button variant="ghost" size="sm" onClick={() => setCoupon(null)} aria-label={tc('remove')}>
                <X className="h-4 w-4" aria-hidden="true" />
              </Button>
            </div>
          ) : (
            <form
              onSubmit={couponForm.handleSubmit((v) => couponMutation.mutate(v.code))}
              className="flex items-end gap-2"
              noValidate
            >
              <Field label={tp('coupon')} error={couponForm.formState.errors.code?.message} className="flex-1">
                <Input placeholder="SAVE20" {...couponForm.register('code')} />
              </Field>
              <Button type="submit" variant="outline" loading={couponMutation.isPending}>
                {tc('apply')}
              </Button>
            </form>
          )}
        </Card>

        {/* Price breakdown */}
        <Card className="p-5">
          <dl className="space-y-2 text-sm">
            <Row label={tp('plan')} value={formatInr(price.base)} />
            {price.discount > 0 && <Row label={tp('coupon')} value={`– ${formatInr(price.discount)}`} muted />}
            <Row label={tp('inclGst')} value={formatInr(price.gst)} muted />
            <div className="flex items-center justify-between border-t border-ink-100 pt-2">
              <dt className="font-semibold text-ink-900">{tp('payable')}</dt>
              <dd className="text-xl font-bold tabular-nums text-ink-900">{formatInr(price.total)}</dd>
            </div>
          </dl>
          <p className="mt-3 inline-flex items-center gap-1.5 text-xs text-ink-400">
            <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />
            {tp('noCardStored')}
          </p>
        </Card>

        {payError && <Alert variant="error">{payError}</Alert>}
      </WizardStep>

      <WizardFooter
        primary={
          <Button
            type="button"
            onClick={() => payMutation.mutate()}
            loading={payMutation.isPending}
            disabled={!effectiveCode}
          >
            {tp('payWith', { gateway: 'Razorpay' })}
          </Button>
        }
      />
    </>
  );
}

function Row({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div className="flex items-center justify-between">
      <dt className={muted ? 'text-ink-400' : 'text-ink-600'}>{label}</dt>
      <dd className={`tabular-nums ${muted ? 'text-ink-500' : 'text-ink-800'}`}>{value}</dd>
    </div>
  );
}
