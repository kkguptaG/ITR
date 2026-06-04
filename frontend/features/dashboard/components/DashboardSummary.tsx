'use client';

// DashboardSummary — the income / deductions / tax-summary trio. Driven by the
// latest return's computed result (per-head income + the tax ladder) and its
// Chapter VI-A deductions.

import Link from 'next/link';
import { Briefcase, Home, LineChart, PiggyBank, Wallet, ChevronRight } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import type { TaxComputationResultDto, DeductionDto } from '@/features/filing/types';

function Row({ label, value, strong, tone }: { label: string; value: string; strong?: boolean; tone?: 'money' | 'payable' }) {
  return (
    <div className={cn('flex items-center justify-between gap-3 py-1.5 text-sm', strong && 'border-t border-ink-100 pt-2 font-semibold')}>
      <span className={strong ? 'text-ink-800' : 'text-ink-500'}>{label}</span>
      <span
        className={cn(
          'tabular-nums',
          tone === 'money' ? 'text-money-700' : tone === 'payable' ? 'text-payable-700' : 'text-ink-900',
          strong && 'text-base',
        )}
      >
        {value}
      </span>
    </div>
  );
}

export function DashboardSummary({
  result,
  deductions,
  returnId,
}: {
  result: TaxComputationResultDto;
  deductions: DeductionDto[];
  returnId: string;
}) {
  const heads = [
    { label: 'Salary', icon: Briefcase, amount: result.salaryNetIncome },
    { label: 'House Property', icon: Home, amount: result.housePropertyNetIncome },
    { label: 'Business / Profession', icon: Wallet, amount: result.businessNetIncome },
    { label: 'Capital Gains', icon: LineChart, amount: result.capitalGainsNetIncome },
    { label: 'Other Sources', icon: PiggyBank, amount: result.otherSourcesNetIncome },
  ].filter((h) => Math.abs(h.amount) > 0);

  const topDeductions = [...deductions]
    .map((d) => ({ section: d.section, amount: d.eligibleAmount ?? d.amount }))
    .filter((d) => d.amount > 0)
    .sort((a, b) => b.amount - a.amount)
    .slice(0, 5);

  const refund = result.refundOrPayable >= 0;

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      {/* Income summary */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <CardTitle>Income summary</CardTitle>
          <Link href={`/returns/${returnId}`} className="text-sm font-medium text-brand-600 hover:text-brand-700">
            Details
          </Link>
        </CardHeader>
        <CardContent>
          {heads.length === 0 ? (
            <p className="py-2 text-sm text-ink-500">No income captured yet.</p>
          ) : (
            <div className="space-y-0.5">
              {heads.map(({ label, icon: Icon, amount }) => (
                <div key={label} className="flex items-center justify-between gap-3 py-1.5 text-sm">
                  <span className="flex items-center gap-2 text-ink-600">
                    <Icon className="h-4 w-4 text-ink-400" aria-hidden="true" />
                    {label}
                  </span>
                  <span className="tabular-nums text-ink-900">{formatInr(amount)}</span>
                </div>
              ))}
              <Row label="Gross total income" value={formatInr(result.grossTotalIncome)} strong />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Top deductions */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <CardTitle>Top deductions</CardTitle>
          <Link href={`/returns/${returnId}/file/deductions`} className="text-sm font-medium text-brand-600 hover:text-brand-700">
            Manage
          </Link>
        </CardHeader>
        <CardContent>
          {topDeductions.length === 0 ? (
            <div className="flex items-start gap-2 rounded-xl bg-brand-50/60 p-3 text-sm text-brand-900">
              <ChevronRight className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <span>No deductions claimed yet — you may be able to save under 80C, 80D and more.</span>
            </div>
          ) : (
            <div className="space-y-0.5">
              {topDeductions.map((d) => (
                <Row key={d.section} label={d.section} value={formatInr(d.amount)} />
              ))}
              <Row label="Total deductions" value={formatInr(result.totalDeductions)} strong />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Tax summary */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <CardTitle>Tax summary</CardTitle>
          <span className="rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-medium text-brand-700">{result.regime} regime</span>
        </CardHeader>
        <CardContent>
          <div className="space-y-0.5">
            <Row label="Total income" value={formatInr(result.grossTotalIncome)} />
            <Row label="Total deductions" value={formatInr(result.totalDeductions)} />
            <Row label="Taxable income" value={formatInr(result.taxableIncome)} />
            <Row label="Total tax" value={formatInr(result.totalTax)} />
            <Row label="TDS / TCS" value={formatInr(result.tdsPaid + result.tcsPaid)} />
            <Row
              label={refund ? 'Estimated refund' : 'Balance payable'}
              value={formatInr(Math.abs(result.refundOrPayable))}
              tone={refund ? 'money' : 'payable'}
              strong
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
