'use client';

// ---------------------------------------------------------------------------
// features/shared/TaxSummaryPanel — line-by-line breakdown of one computation.
//
// Renders the canonical statutory ladder (gross total income → deductions →
// taxable income → tax → cess → rebate 87A → surcharge → total tax → taxes
// already paid → refund/payable) with Indian-grouped INR. Self-contained: takes
// a ComputationView, no feature-local imports. Used by the wizard summary step,
// the return detail page, and the regime-compare expander.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';
import { formatInr, toNumber } from '@/lib/format';
import type { ComputationView } from './types';

interface Row {
  labelKey: string;
  value: number;
  /** Subtract sign shown as a leading minus for deductions/taxes-paid. */
  negative?: boolean;
  /** Visual emphasis for subtotal rows. */
  strong?: boolean;
  /** Hide when zero (keeps the panel compact for simple returns). */
  hideWhenZero?: boolean;
}

export interface TaxSummaryPanelProps {
  computation: ComputationView;
  /** Hide the final refund/payable footer (e.g. when shown elsewhere). */
  hideOutcome?: boolean;
  className?: string;
}

export function TaxSummaryPanel({ computation, hideOutcome, className }: TaxSummaryPanelProps) {
  const t = useTranslations('shared');

  const taxesPaid = toNumber(computation.tdsPaid) + toNumber(computation.advanceTax);
  const outcome = toNumber(computation.refundOrPayable);
  const isRefund = outcome >= 0;

  const rows: Row[] = [
    { labelKey: 'grossTotalIncome', value: toNumber(computation.grossTotalIncome) },
    { labelKey: 'totalDeductions', value: toNumber(computation.totalDeductions), negative: true, hideWhenZero: true },
    { labelKey: 'taxableIncome', value: toNumber(computation.taxableIncome), strong: true },
    { labelKey: 'taxBeforeCess', value: toNumber(computation.taxBeforeCess) },
    { labelKey: 'rebate87A', value: toNumber(computation.rebate87A), negative: true, hideWhenZero: true },
    { labelKey: 'surcharge', value: toNumber(computation.surcharge), hideWhenZero: true },
    { labelKey: 'cess', value: toNumber(computation.cess) },
    { labelKey: 'totalTax', value: toNumber(computation.totalTax), strong: true },
    { labelKey: 'taxesPaid', value: taxesPaid, negative: true, hideWhenZero: true },
  ];

  return (
    <div className={cn('overflow-hidden rounded-xl border border-ink-200', className)}>
      <dl className="divide-y divide-ink-100">
        {rows
          .filter((r) => !(r.hideWhenZero && r.value === 0))
          .map((r) => (
            <div
              key={r.labelKey}
              className={cn(
                'flex items-center justify-between px-4 py-2.5 text-sm',
                r.strong && 'bg-ink-50',
              )}
            >
              <dt className={cn('text-ink-600', r.strong && 'font-semibold text-ink-900')}>
                {t(r.labelKey)}
              </dt>
              <dd
                className={cn(
                  'tabular-nums text-ink-900',
                  r.strong && 'font-semibold',
                  r.negative && 'text-ink-500',
                )}
              >
                {r.negative && r.value !== 0 ? '− ' : ''}
                {formatInr(r.value)}
              </dd>
            </div>
          ))}
      </dl>

      {!hideOutcome && (
        <div
          className={cn(
            'flex items-center justify-between px-4 py-3',
            isRefund ? 'bg-money-50' : 'bg-payable-50',
          )}
        >
          <span
            className={cn(
              'text-sm font-semibold',
              isRefund ? 'text-money-700' : 'text-payable-700',
            )}
          >
            {isRefund ? t('refundDue') : t('taxPayable')}
          </span>
          <span
            className={cn(
              'text-base font-bold tabular-nums',
              isRefund ? 'text-money-700' : 'text-payable-700',
            )}
          >
            {formatInr(Math.abs(outcome))}
          </span>
        </div>
      )}
    </div>
  );
}
