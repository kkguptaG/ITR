'use client';

// RefundTrackerRow — one return on the Refund Tracker page. For a filed/processed
// return it shows the ACTUAL refund/demand state (from the refund module); otherwise
// the computed estimate from the return summary.

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ChevronRight } from 'lucide-react';
import { Card, Badge, StatusBadge } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, formatAssessmentYear } from '@/lib/format';
import { formatItrType } from '@/features/returns/helpers';
import type { ReturnSummaryDto } from '@/features/returns/types';
import { useRefundStatus } from '../useRefund';

export function RefundTrackerRow({ r }: { r: ReturnSummaryDto }) {
  const t = useTranslations('refundTracker');
  const tr = useTranslations('refund');
  const ts = useTranslations('status');

  const isFiledish = r.status === 'Filed' || r.status === 'Processed';
  const refund = useRefundStatus(r.id, isFiledish).data;

  let amount: number;
  let amountLabel: string;
  let tone: 'money' | 'payable' | 'neutral';
  let stateText: string;

  if (refund?.isProcessed) {
    const isDemand = refund.status === 'DemandDetermined';
    amount = isDemand ? refund.demandAmount : refund.determinedAmount;
    amountLabel = isDemand ? t('payable') : t('refund');
    tone = isDemand ? 'payable' : 'money';
    stateText = tr(`status.${refund.status}`);
  } else {
    const rp = r.refundOrPayable ?? 0;
    amount = Math.abs(rp);
    const isRefund = rp >= 0;
    amountLabel = isRefund ? t('refund') : t('payable');
    tone = isRefund ? 'money' : 'payable';
    stateText = isFiledish ? (r.eVerifiedAt ? t('processing') : t('filedVerify')) : t('estimated');
  }

  return (
    <Link href={`/returns/${r.id}`} className="block">
      <Card className="flex items-center gap-4 p-4 transition-colors hover:border-brand-300">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-semibold text-ink-900">{formatAssessmentYear(r.assessmentYear)}</span>
            {r.itrType && <Badge tone="brand">{formatItrType(r.itrType)}</Badge>}
            <StatusBadge status={r.status}>{ts(r.status)}</StatusBadge>
          </div>
          <p className="mt-0.5 text-xs text-ink-500">{stateText}</p>
        </div>
        <div className="text-right">
          <p className="text-xs text-ink-500">{amountLabel}</p>
          <p
            className={cn(
              'text-lg font-semibold tabular-nums',
              tone === 'money' ? 'text-money-700' : tone === 'payable' ? 'text-payable-700' : 'text-ink-900',
            )}
          >
            {formatInr(amount)}
          </p>
        </div>
        <ChevronRight className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
      </Card>
    </Link>
  );
}
