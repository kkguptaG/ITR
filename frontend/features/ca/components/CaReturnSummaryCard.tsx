'use client';

// CaReturnSummaryCard — the taxpayer return snapshot a CA sees inside the review
// panel: taxpayer, AY, ITR type, regime, status, and the computed refund/payable.
// Data comes from the assignment detail (CaReturnSummaryDto) — the CA does not
// hit the user-scoped /returns endpoint.

import { useTranslations } from 'next-intl';
import { User2 } from 'lucide-react';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Badge,
  StatusBadge,
} from '@/components/ui';
import { formatInr, formatDate, formatAssessmentYear } from '@/lib/format';
import { formatItrType, formatRegime } from '@/features/returns/helpers';
import type { CaReturnSummaryDto } from '../types';
import { refundOrPayableNumber } from '../helpers';

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 py-2.5">
      <dt className="text-sm text-ink-500">{label}</dt>
      <dd className="text-sm font-medium text-ink-900">{children}</dd>
    </div>
  );
}

export function CaReturnSummaryCard({ summary }: { summary: CaReturnSummaryDto }) {
  const t = useTranslations('caReview');
  const ts = useTranslations('status');

  const amount = refundOrPayableNumber(summary.refundOrPayable);
  const hasComputation =
    summary.refundOrPayable !== null && summary.refundOrPayable !== undefined;
  const isRefund = amount >= 0;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-3">
        <span className="flex h-10 w-10 items-center justify-center rounded-full bg-brand-50 text-brand-600">
          <User2 className="h-5 w-5" aria-hidden="true" />
        </span>
        <div>
          <CardTitle>{summary.taxpayerName || t('unnamedTaxpayer')}</CardTitle>
          <p className="text-sm text-ink-500">
            {formatAssessmentYear(summary.assessmentYear)}
            {summary.itrType ? ` · ${formatItrType(summary.itrType)}` : ''}
          </p>
        </div>
      </CardHeader>
      <CardContent>
        <dl className="divide-y divide-ink-100">
          <Row label={t('summaryStatus')}>
            <StatusBadge status={summary.status}>{ts(summary.status)}</StatusBadge>
          </Row>
          <Row label={t('summaryItrType')}>
            {summary.itrType ? (
              <Badge tone="neutral">{formatItrType(summary.itrType)}</Badge>
            ) : (
              <span className="text-ink-400">{t('itrPending')}</span>
            )}
          </Row>
          <Row label={t('summaryRegime')}>{formatRegime(summary.regime)}</Row>
          <Row label={t('summarySubmitted')}>{formatDate(summary.submittedAt)}</Row>
          <Row label={t('summaryCreated')}>{formatDate(summary.createdAt)}</Row>
        </dl>

        {/* Computed outcome — the headline number for the CA's check. */}
        <div
          className={`mt-4 rounded-xl border p-4 ${
            !hasComputation
              ? 'border-ink-200 bg-ink-50'
              : isRefund
                ? 'border-money-200 bg-money-50'
                : 'border-payable-200 bg-payable-50'
          }`}
        >
          {hasComputation ? (
            <>
              <p
                className={`text-xs font-medium uppercase tracking-wide ${
                  isRefund ? 'text-money-700' : 'text-payable-700'
                }`}
              >
                {isRefund ? t('refundDue') : t('amountPayable')}
              </p>
              <p
                className={`mt-1 text-2xl font-semibold ${
                  isRefund ? 'text-money-800' : 'text-payable-800'
                }`}
              >
                {formatInr(Math.abs(amount))}
              </p>
            </>
          ) : (
            <p className="text-sm text-ink-500">{t('notComputed')}</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
