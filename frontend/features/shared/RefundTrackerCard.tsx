'use client';

// ---------------------------------------------------------------------------
// features/shared/RefundTrackerCard — refund / tax-payable headline.
//
// Self-contained: takes a signed amount (positive = refund due, negative =
// payable) OR a ComputationView to read it from, plus the return status and a
// friendly AY label. No feature-local DTO imports, so it is import-safe from
// the dashboard, the wizard summary, or anywhere a single headline is wanted.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  Calculator,
  ChevronRight,
} from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent, StatusBadge } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, formatAssessmentYear, toNumber } from '@/lib/format';
import type { ReturnStatus } from '@/lib/api-types';
import type { ComputationView } from './types';

export interface RefundTrackerCardProps {
  /** Return lifecycle status — drives the badge. */
  status: ReturnStatus;
  /** Assessment-year code, e.g. "AY2025-26" (rendered as "AY 2025-26"). */
  assessmentYear?: string | null;
  /**
   * Either pass a computation to read refundOrPayable from, or pass the signed
   * `amount` directly. If neither is set we render the "no computation" nudge.
   */
  computation?: ComputationView | null;
  /** Signed amount: positive = refund due, negative = payable. */
  amount?: number | string | null;
  /** Localized status label (already translated). Optional — badge shows raw status otherwise. */
  statusLabel?: string;
  /** Optional CTA link to the return. */
  href?: string;
  /** Optional translated CTA label; defaults to returns.viewReturn. */
  ctaLabel?: string;
  className?: string;
}

export function RefundTrackerCard({
  status,
  assessmentYear,
  computation,
  amount,
  statusLabel,
  href,
  ctaLabel,
  className,
}: RefundTrackerCardProps) {
  const t = useTranslations('returns');

  const raw =
    amount !== undefined && amount !== null
      ? toNumber(amount)
      : computation
        ? toNumber(computation.refundOrPayable)
        : null;

  const hasValue = raw !== null;
  const isRefund = (raw ?? 0) >= 0;
  const magnitude = Math.abs(raw ?? 0);

  return (
    <Card className={className}>
      <CardHeader className="flex flex-row items-center justify-between gap-3 space-y-0">
        <CardTitle>{t('refundTracker')}</CardTitle>
        <StatusBadge status={status}>{statusLabel ?? status}</StatusBadge>
      </CardHeader>
      <CardContent>
        {!hasValue ? (
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

        {(assessmentYear || href) && (
          <div className="mt-4 flex items-center justify-between text-sm">
            <span className="text-ink-500">{formatAssessmentYear(assessmentYear)}</span>
            {href && (
              <Link
                href={href}
                className="inline-flex items-center gap-1 font-medium text-brand-600 hover:text-brand-700"
              >
                {ctaLabel ?? t('viewReturn')}
                <ChevronRight className="h-4 w-4" aria-hidden="true" />
              </Link>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
