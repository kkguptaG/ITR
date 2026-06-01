'use client';

// ---------------------------------------------------------------------------
// /payments — payment history, GST invoices, wallet, and the checkout entry.
//   • Tabs: "History" (payments table + invoice download) and "Wallet"
//     (balance + ledger).
//   • A "Pay a filing fee" panel lets the user pick a return + plan and launch
//     the reusable CheckoutDialog (the same component the wizard uses).
//   • A standalone coupon box previews a discount against a plan.
// All data via TanStack Query against /payments, /wallet, /pricing/plans.
// ---------------------------------------------------------------------------

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslations } from 'next-intl';
import { CreditCard, Receipt } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
  Select,
  Button,
  Spinner,
  Alert,
  EmptyState,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatInr, formatAssessmentYear } from '@/lib/format';
import { listReturns } from '@/features/returns/api';
import { PaymentHistoryTable } from '@/features/payments/components/PaymentHistoryTable';
import { WalletPanel } from '@/features/payments/components/WalletPanel';
import { CheckoutDialog } from '@/features/payments/components/CheckoutDialog';
import { CouponField } from '@/features/payments/components/CouponField';
import { listPayments, listPlans, paymentsKeys } from '@/features/payments/api';
import { formatItrType } from '@/features/returns/helpers';

const PAGE_SIZE = 20;

export default function PaymentsPage() {
  const t = useTranslations('payments');
  const tc = useTranslations('common');

  const [tab, setTab] = useState('history');
  const [page, setPage] = useState(1);
  const [checkoutOpen, setCheckoutOpen] = useState(false);

  const paymentsQuery = useQuery({
    queryKey: paymentsKeys.list(page, PAGE_SIZE),
    queryFn: () => listPayments(page, PAGE_SIZE),
    placeholderData: (prev) => prev,
  });

  const data = paymentsQuery.data;
  const payments = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-ink-900">{t('title')}</h1>
        <p className="mt-1 text-sm text-ink-500">
          Pay your filing fee, download GST invoices, and manage your wallet credit.
        </p>
      </header>

      <PayFilingFeePanel onCheckout={() => setCheckoutOpen(true)} checkoutOpen={checkoutOpen} onCloseCheckout={() => setCheckoutOpen(false)} />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="wallet">Wallet</TabsTrigger>
        </TabsList>

        <TabsContent value="history">
          {paymentsQuery.isLoading ? (
            <div className="flex justify-center py-12">
              <Spinner label={tc('loading')} />
            </div>
          ) : paymentsQuery.isError ? (
            <Alert variant="error" title="Couldn’t load your payments">
              {paymentsQuery.error instanceof ApiError
                ? (paymentsQuery.error.problem.detail ?? paymentsQuery.error.message)
                : 'Please try again.'}
            </Alert>
          ) : payments.length === 0 ? (
            <EmptyState icon={Receipt} title={t('emptyTitle')} description={t('emptyBody')} />
          ) : (
            <div className="space-y-4">
              <PaymentHistoryTable payments={payments} />
              {totalPages > 1 && (
                <div className="flex items-center justify-between text-sm text-ink-500">
                  <span>
                    Page {page} of {totalPages} · {total} payment{total === 1 ? '' : 's'}
                  </span>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={page <= 1}
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                    >
                      {tc('back')}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={page >= totalPages}
                      onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    >
                      {tc('next')}
                    </Button>
                  </div>
                </div>
              )}
            </div>
          )}
        </TabsContent>

        <TabsContent value="wallet">
          <WalletPanel />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// --------------------------------------------------------------- pay panel

function PayFilingFeePanel({
  onCheckout,
  checkoutOpen,
  onCloseCheckout,
}: {
  onCheckout: () => void;
  checkoutOpen: boolean;
  onCloseCheckout: () => void;
}) {
  const t = useTranslations('payments');

  const plansQuery = useQuery({ queryKey: paymentsKeys.plans, queryFn: listPlans });
  // Returns the user might pay for (any not yet filed/processed).
  const returnsQuery = useQuery({
    queryKey: ['returns', 'list', { forPayment: true }],
    queryFn: () => listReturns({ page: 1, pageSize: 50 }),
  });

  const plans = useMemo(() => plansQuery.data ?? [], [plansQuery.data]);
  const payableReturns = useMemo(
    () =>
      (returnsQuery.data?.items ?? []).filter(
        (r) => r.status !== 'Filed' && r.status !== 'Processed',
      ),
    [returnsQuery.data],
  );

  const [returnId, setReturnId] = useState('');
  const [planCode, setPlanCode] = useState('');

  // Default selections once data loads.
  const effectiveReturnId = returnId || payableReturns[0]?.id || '';
  const effectivePlanCode = planCode || plans[0]?.code || '';
  const selectedPlan = plans.find((p) => p.code === effectivePlanCode);

  const ready = !!effectiveReturnId && !!effectivePlanCode;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('payFee')}</CardTitle>
        <CardDescription>
          Choose a return and a plan, then pay securely. {t('inclGst')}.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {plansQuery.isLoading || returnsQuery.isLoading ? (
          <div className="flex justify-center py-4">
            <Spinner label="Loading plans…" />
          </div>
        ) : payableReturns.length === 0 ? (
          <EmptyState
            icon={CreditCard}
            title="No returns awaiting payment"
            description="Start and compute a return first — then come back to pay the filing fee."
          />
        ) : (
          <>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <label htmlFor="pay-return" className="text-sm font-medium text-ink-700">
                  Return
                </label>
                <Select
                  id="pay-return"
                  value={effectiveReturnId}
                  onChange={(e) => setReturnId(e.target.value)}
                >
                  {payableReturns.map((r) => (
                    <option key={r.id} value={r.id}>
                      {formatAssessmentYear(r.assessmentYear)} · {formatItrType(r.itrType)}
                    </option>
                  ))}
                </Select>
              </div>
              <div className="space-y-1.5">
                <label htmlFor="pay-plan" className="text-sm font-medium text-ink-700">
                  {t('plan')}
                </label>
                <Select id="pay-plan" value={effectivePlanCode} onChange={(e) => setPlanCode(e.target.value)}>
                  {plans.map((p) => (
                    <option key={p.code} value={p.code}>
                      {p.name} — {formatInr(p.price)}
                    </option>
                  ))}
                </Select>
              </div>
            </div>

            {selectedPlan && selectedPlan.features.length > 0 && (
              <ul className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-ink-500">
                {selectedPlan.features.map((f) => (
                  <li key={f} className="flex items-center gap-1.5">
                    <span className="h-1.5 w-1.5 rounded-full bg-brand-400" aria-hidden="true" />
                    {f}
                  </li>
                ))}
              </ul>
            )}

            {/* Standalone coupon preview (the authoritative discount is applied at checkout). */}
            <CouponField planCode={effectivePlanCode} className="max-w-md" />

            <div className="pt-1">
              <Button onClick={onCheckout} disabled={!ready}>
                {t('payFee')}
              </Button>
            </div>
          </>
        )}
      </CardContent>

      {ready && (
        <CheckoutDialog
          open={checkoutOpen}
          onClose={onCloseCheckout}
          returnId={effectiveReturnId}
          planCode={effectivePlanCode}
          planName={selectedPlan?.name}
        />
      )}
    </Card>
  );
}
