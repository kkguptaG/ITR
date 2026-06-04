'use client';

// ---------------------------------------------------------------------------
// /refund-tracker — every return the user has, with its refund (or demand) state:
// the actual ITD status for filed/processed returns, the computed estimate
// otherwise. Header KPIs sum the expected refunds.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { FileText, Wallet } from 'lucide-react';
import { Spinner, EmptyState } from '@/components/ui';
import { formatInr } from '@/lib/format';
import { listReturns, returnsKeys } from '@/features/returns';
import { KpiCard } from '@/features/dashboard/components/KpiCard';
import { RefundTrackerRow } from '@/features/refunds/components/RefundTrackerRow';

export default function RefundTrackerPage() {
  const t = useTranslations('refundTracker');

  const q = useQuery({
    queryKey: returnsKeys.list({ page: 1, pageSize: 50 }),
    queryFn: () => listReturns({ page: 1, pageSize: 50 }),
  });
  const items = q.data?.items ?? [];
  const expected = items.reduce((s, r) => s + Math.max(0, r.refundOrPayable ?? 0), 0);

  if (q.isLoading) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner size={28} />
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-ink-900">{t('title')}</h1>
        <p className="mt-1 text-sm text-ink-500">{t('subtitle')}</p>
      </div>

      {items.length === 0 ? (
        <EmptyState icon={Wallet} title={t('title')} description={t('empty')} />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <KpiCard icon={FileText} tone="brand" label={t('kpiReturns')} value={String(items.length)} />
            <KpiCard icon={Wallet} tone="money" label={t('kpiExpected')} value={formatInr(expected)} />
          </div>
          <div className="space-y-3">
            {items.map((r) => (
              <RefundTrackerRow key={r.id} r={r} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
