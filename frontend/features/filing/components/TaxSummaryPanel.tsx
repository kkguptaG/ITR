'use client';

// ---------------------------------------------------------------------------
// TaxSummaryPanel — the headline result for one regime: a clean waterfall from
// gross total income → deductions → taxable income → tax (incl. rebate/cess/
// surcharge) → taxes paid → refund or payable, with the full trace expandable.
// Renders from a TaxComputationResultDto (one regime).
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { ArrowDownToLine, ArrowUpFromLine } from 'lucide-react';
import { Card } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import { formatRegime } from '@/features/returns/helpers';
import type { TaxComputationResultDto } from '../types';
import { ComputationTrace } from './ComputationTrace';

function Line({
  label,
  value,
  tone = 'default',
  strong,
  indent,
}: {
  label: string;
  value: number;
  tone?: 'default' | 'subtract' | 'muted';
  strong?: boolean;
  indent?: boolean;
}) {
  return (
    <div className={cn('flex items-center justify-between gap-3 py-1.5', indent && 'pl-4')}>
      <span className={cn('text-sm', tone === 'muted' ? 'text-ink-400' : 'text-ink-600', strong && 'font-medium text-ink-900')}>
        {label}
      </span>
      <span
        className={cn(
          'text-sm tabular-nums',
          tone === 'subtract' ? 'text-ink-500' : 'text-ink-900',
          strong && 'font-semibold',
        )}
      >
        {tone === 'subtract' ? `– ${formatInr(value)}` : formatInr(value)}
      </span>
    </div>
  );
}

export function TaxSummaryPanel({ comp }: { comp: TaxComputationResultDto }) {
  const t = useTranslations('wizard');
  const refund = comp.refundOrPayable;
  const isRefund = refund >= 0;

  return (
    <Card className="overflow-hidden">
      <div className="border-b border-ink-100 px-5 py-3">
        <span className="text-xs font-medium uppercase tracking-wide text-ink-400">
          {formatRegime(comp.regime)}
        </span>
      </div>

      <div className="divide-y divide-ink-100 px-5">
        <div className="py-1.5">
          <Line label={t('grossIncome')} value={comp.grossTotalIncome} strong />
          <Line label={t('totalDeductions')} value={comp.totalDeductions} tone="subtract" />
        </div>
        <div className="py-1.5">
          <Line label={t('taxableIncome')} value={comp.taxableIncome} strong />
        </div>
        <div className="py-1.5">
          <Line label={t('taxBeforeRebate')} value={comp.taxBeforeRebate} />
          {comp.rebate87A > 0 && <Line label={t('rebate87A')} value={comp.rebate87A} tone="subtract" indent />}
          {comp.surcharge > 0 && <Line label={t('surcharge')} value={comp.surcharge} indent />}
          <Line label={t('cess')} value={comp.cess} indent />
          {comp.alternativeMinimumTax > 0 && <Line label="Alternate Minimum Tax (s.115JC)" value={comp.alternativeMinimumTax} indent />}
          {comp.amtCreditSetOff > 0 && <Line label="Less: AMT credit set off (s.115JD)" value={comp.amtCreditSetOff} tone="subtract" indent />}
          {comp.relief89 > 0 && <Line label="Less: relief u/s 89 (arrears)" value={comp.relief89} tone="subtract" indent />}
          {comp.relief90And91 > 0 && <Line label="Less: relief u/s 90/91 (foreign tax)" value={comp.relief90And91} tone="subtract" indent />}
          {comp.interestPenalty > 0 && <Line label={t('interestPenalty')} value={comp.interestPenalty} indent />}
          <Line label={t('totalTax')} value={comp.totalTax} strong />
          {comp.amtCreditGenerated > 0 && <Line label="AMT credit carried forward (s.115JD)" value={comp.amtCreditGenerated} tone="muted" indent />}
        </div>
        <div className="py-1.5">
          <Line label={t('tdsPaid')} value={comp.tdsPaid} tone="subtract" />
          {comp.advanceTax > 0 && <Line label={t('advanceTax')} value={comp.advanceTax} tone="subtract" />}
        </div>
      </div>

      {/* Net result */}
      <div
        className={cn(
          'flex items-center justify-between gap-3 px-5 py-4',
          isRefund ? 'bg-money-50' : 'bg-payable-50',
        )}
      >
        <span className="inline-flex items-center gap-2 text-sm font-medium text-ink-700">
          {isRefund ? (
            <ArrowDownToLine className="h-4 w-4 text-money-600" aria-hidden="true" />
          ) : (
            <ArrowUpFromLine className="h-4 w-4 text-payable-700" aria-hidden="true" />
          )}
          {isRefund ? t('refundDue') : t('taxPayable')}
        </span>
        <span
          className={cn(
            'text-2xl font-bold tabular-nums',
            isRefund ? 'text-money-700' : 'text-payable-700',
          )}
        >
          {formatInr(Math.abs(refund))}
        </span>
      </div>

      <div className="px-5 pb-5 pt-4">
        <ComputationTrace trace={comp.trace} />
      </div>
    </Card>
  );
}
