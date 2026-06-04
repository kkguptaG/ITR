'use client';

// ---------------------------------------------------------------------------
// RefundTrackerCard — refund / payable headline for the latest return.
// Reads the signed refundOrPayable from the return's latest computation
// (positive = refund due, negative = tax payable). When no computation exists
// yet it nudges the user to finish the return so we can compute.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowDownToLine, ArrowUpFromLine, Calculator, ChevronRight } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, formatAssessmentYear, toNumber } from '@/lib/format';
import type { ReturnSummaryDto, TaxComputationDto } from '@/features/returns/types';
import { ReturnStatusBadge } from '@/features/returns/components/ReturnStatusBadge';
import { returnHref } from '@/features/returns/helpers';
import { useRefundStatus } from '@/features/refunds';
import type { RefundStatusDto } from '@/features/refunds';

export interface RefundTrackerCardProps {
  latest: ReturnSummaryDto;
  computation: TaxComputationDto | null;
}

export function RefundTrackerCard({ latest, computation }: RefundTrackerCardProps) {
  const t = useTranslations('returns');

  // Once the return is filed/processed, surface the ACTUAL refund/demand state (determined → paid,
  // demand, or no-refund) from the refund module — not just the pre-filing computed estimate.
  const isFiledish = latest.status === 'Filed' || latest.status === 'Processed';
  const refund = useRefundStatus(latest.id, isFiledish).data;
  const showActual = !!refund?.isProcessed;

  const hasComputation = computation !== null;
  const amount = hasComputation ? toNumber(computation.refundOrPayable) : 0;
  const isRefund = amount >= 0;
  const magnitude = Math.abs(amount);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <CardTitle>{t('refundTracker')}</CardTitle>
        <ReturnStatusBadge status={latest.status} />
      </CardHeader>
      <CardContent>
        {showActual && refund ? (
          <ActualRefundBox refund={refund} />
        ) : !hasComputation ? (
          <div className="flex items-start gap-3 rounded-xl bg-ink-50 p-4">
            <Calculator className="mt-0.5 h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-ink-800">{t('noComputationTitle')}</p>
              <p className="mt-0.5 text-sm text-ink-500">{t('noComputationBody')}</p>
            </div>
          </div>
        ) : (
          <div className={cn('rounded-xl p-4', isRefund ? 'bg-money-50' : 'bg-payable-50')}>
            <div className="flex items-center gap-2">
              {isRefund ? (
                <ArrowDownToLine className="h-5 w-5 text-money-600" aria-hidden="true" />
              ) : (
                <ArrowUpFromLine className="h-5 w-5 text-payable-600" aria-hidden="true" />
              )}
              <span
                className={cn(
                  'text-sm font-medium',
                  isRefund ? 'text-money-700' : 'text-payable-700',
                )}
              >
                {isRefund ? t('refundExpected') : t('taxPayable')}
              </span>
            </div>
            <p
              className={cn(
                'mt-1 text-3xl font-semibold tabular-nums',
                isRefund ? 'text-money-700' : 'text-payable-700',
              )}
            >
              {formatInr(magnitude)}
            </p>
          </div>
        )}

        <div className="mt-4 flex items-center justify-between text-sm">
          <span className="text-ink-500">{formatAssessmentYear(latest.assessmentYear)}</span>
          <Link
            href={returnHref(latest)}
            className="inline-flex items-center gap-1 font-medium text-brand-600 hover:text-brand-700"
          >
            {t('viewReturn')}
            <ChevronRight className="h-4 w-4" aria-hidden="true" />
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}

const REFUND_FLOW_STATES = ['RefundDetermined', 'RefundSentToBank', 'RefundPaid', 'RefundFailed'];

/** The post-processing refund/demand headline (real ITD state), shown once the return is processed. */
function ActualRefundBox({ refund }: { refund: RefundStatusDto }) {
  const tr = useTranslations('refund');
  const label = tr(`status.${refund.status}`);

  if (refund.status === 'DemandDetermined') {
    return (
      <div className="rounded-xl bg-payable-50 p-4">
        <div className="flex items-center gap-2">
          <ArrowUpFromLine className="h-5 w-5 text-payable-600" aria-hidden="true" />
          <span className="text-sm font-medium text-payable-700">{label}</span>
        </div>
        <p className="mt-1 text-3xl font-semibold tabular-nums text-payable-700">
          {formatInr(refund.demandAmount)}
        </p>
      </div>
    );
  }

  if (REFUND_FLOW_STATES.includes(refund.status)) {
    const failed = refund.status === 'RefundFailed';
    return (
      <div className={cn('rounded-xl p-4', failed ? 'bg-red-50' : 'bg-money-50')}>
        <div className="flex items-center gap-2">
          <ArrowDownToLine className={cn('h-5 w-5', failed ? 'text-red-600' : 'text-money-600')} aria-hidden="true" />
          <span className={cn('text-sm font-medium', failed ? 'text-red-700' : 'text-money-700')}>{label}</span>
        </div>
        <p className={cn('mt-1 text-3xl font-semibold tabular-nums', failed ? 'text-red-700' : 'text-money-700')}>
          {formatInr(refund.determinedAmount)}
        </p>
      </div>
    );
  }

  // NoRefundOrDemand / RefundAdjusted — neutral.
  return (
    <div className="rounded-xl bg-ink-50 p-4">
      <span className="text-sm font-medium text-ink-700">{label}</span>
      {refund.status === 'RefundAdjusted' && (
        <p className="mt-1 text-2xl font-semibold tabular-nums text-ink-800">
          {formatInr(refund.determinedAmount)}
        </p>
      )}
    </div>
  );
}
