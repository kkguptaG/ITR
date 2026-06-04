'use client';

// ---------------------------------------------------------------------------
// ComputationDashboard — the Webtel-style line-by-line computation that is ALSO
// the navigation hub. Every income / deduction / prepaid-tax line is clickable
// and routes straight to the form that captures it; the tax-stage lines (tax,
// surcharge, cess, rebate, interest) are computed and read-only. One screen,
// the whole return — "click a line, fill its form".
//
// Income heads carry their net-income amount (from the compute result's per-head
// fields); clicking a head deep-links the Income step to that section.
// ---------------------------------------------------------------------------

import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight, Calculator, Scale } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, Spinner, Badge, Button } from '@/components/ui';
import { formatInr } from '@/lib/format';
import { cn } from '@/lib/utils';
import { computeTax, filingKeys } from '../api';
import type { Regime, TaxComputationResultDto } from '../types';

type RowKind = 'head' | 'subtotal' | 'deduction' | 'tax' | 'prepaid' | 'result' | 'minor';

interface Row {
  key: string;
  label: string;
  amount: number;
  kind: RowKind;
  /** Where clicking the row navigates (omit for read-only computed rows). */
  href?: string;
  /** Scroll to an element id on the current page instead of navigating. */
  scrollTo?: string;
  indent?: boolean;
}

export function ComputationDashboard({
  returnId,
  detail,
}: {
  returnId: string;
  detail: { regime: Regime | null };
}) {
  const router = useRouter();
  const computeQuery = useQuery({
    queryKey: filingKeys.compute(returnId),
    queryFn: () => computeTax({ returnId }),
    retry: false,
    staleTime: 10_000,
  });

  const data = computeQuery.data;
  const regime = detail.regime ?? data?.recommendedRegime ?? 'New';
  const c: TaxComputationResultDto | undefined = data
    ? regime === 'Old'
      ? data.old
      : data.new
    : undefined;

  const incomeHref = (focus: string) => `/returns/${returnId}/file/income?focus=${focus}`;

  // Build the computation rows from the chosen-regime result (zeros when not yet computed).
  const v = (n: number | undefined) => n ?? 0;
  const rows: Row[] = [
    { key: 'salary', label: 'Salary', amount: v(c?.salaryNetIncome), kind: 'head' as const, href: incomeHref('salary') },
    { key: 'business', label: 'Income from Business or Profession', amount: v(c?.businessNetIncome), kind: 'head' as const, href: incomeHref('business') },
    { key: 'house', label: 'Income from House Property', amount: v(c?.housePropertyNetIncome), kind: 'head' as const, href: incomeHref('house') },
    { key: 'cg', label: 'Capital Gains', amount: v(c?.capitalGainsNetIncome), kind: 'head' as const, href: incomeHref('capitalGains') },
    { key: 'other', label: 'Income from Other Sources', amount: v(c?.otherSourcesNetIncome), kind: 'head' as const, href: incomeHref('other') },
    { key: 'gti', label: 'Gross Total Income', amount: v(c?.grossTotalIncome), kind: 'subtotal' as const },
    { key: 'via', label: 'Less: Deductions under Chapter VI-A', amount: v(c?.totalDeductions), kind: 'deduction' as const, href: `/returns/${returnId}/file/deductions` },
    { key: 'taxable', label: 'Taxable Income', amount: v(c?.taxableIncome), kind: 'subtotal' as const },
    { key: 'taxbefore', label: 'Income Tax', amount: v(c?.taxBeforeRebate), kind: 'tax' as const },
    ...(v(c?.rebate87A) > 0 ? [{ key: 'r87a', label: 'Less: Rebate u/s 87A', amount: v(c?.rebate87A), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.surcharge) > 0 ? [{ key: 'sur', label: 'Add: Surcharge', amount: v(c?.surcharge), kind: 'tax' as const, indent: true }] : []),
    { key: 'cess', label: 'Add: Health & Education Cess', amount: v(c?.cess), kind: 'tax' as const, indent: true },
    ...(v(c?.alternativeMinimumTax) > 0 ? [{ key: 'amt', label: 'Alternate Minimum Tax (s.115JC)', amount: v(c?.alternativeMinimumTax), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.relief89) > 0 ? [{ key: 'rel89', label: 'Less: Relief u/s 89 (arrears)', amount: v(c?.relief89), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.relief90And91) > 0 ? [{ key: 'rel90', label: 'Less: Relief u/s 90/91 (foreign tax)', amount: v(c?.relief90And91), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.interestPenalty) > 0 ? [{ key: 'int', label: 'Add: Interest u/s 234A/B/C', amount: v(c?.interestPenalty), kind: 'tax' as const, indent: true }] : []),
    { key: 'totaltax', label: 'Total Tax Liability', amount: v(c?.totalTax), kind: 'subtotal' as const },
    { key: 'tds', label: 'Less: TDS credit', amount: v(c?.tdsPaid) - v(c?.tcsPaid), kind: 'prepaid' as const, scrollTo: 'taxes-paid' },
    ...(v(c?.tcsPaid) > 0 ? [{ key: 'tcs', label: 'Less: TCS credit', amount: v(c?.tcsPaid), kind: 'prepaid' as const, scrollTo: 'taxes-paid', indent: true }] : []),
    ...(v(c?.advanceTax) - v(c?.selfAssessmentTaxPaid) > 0 ? [{ key: 'adv', label: 'Less: Advance tax', amount: v(c?.advanceTax) - v(c?.selfAssessmentTaxPaid), kind: 'prepaid' as const, scrollTo: 'taxes-paid', indent: true }] : []),
    ...(v(c?.selfAssessmentTaxPaid) > 0 ? [{ key: 'sat', label: 'Less: Self-assessment tax', amount: v(c?.selfAssessmentTaxPaid), kind: 'prepaid' as const, scrollTo: 'taxes-paid', indent: true }] : []),
  ];

  // Losses / credits carried forward to next year (minor, shown only when present).
  const carried: Row[] = [
    { key: 'cf-hp', label: 'House-property loss carried forward (s.71B)', amount: v(c?.housePropertyLossCarriedForward), kind: 'minor' as const },
    { key: 'cf-bus', label: 'Business loss carried forward (s.72)', amount: v(c?.businessLossCarriedForward), kind: 'minor' as const },
    { key: 'cf-spec', label: 'Speculative loss carried forward (s.73)', amount: v(c?.speculativeLossCarriedForward), kind: 'minor' as const },
    { key: 'cf-stcl', label: 'Short-term capital loss carried forward (s.74)', amount: v(c?.shortTermCapitalLossCarriedForward), kind: 'minor' as const },
    { key: 'cf-ltcl', label: 'Long-term capital loss carried forward (s.74)', amount: v(c?.longTermCapitalLossCarriedForward), kind: 'minor' as const },
    { key: 'cf-ud', label: 'Unabsorbed depreciation carried forward (s.32(2))', amount: v(c?.unabsorbedDepreciationCarriedForward), kind: 'minor' as const },
  ].filter((r) => r.amount > 0);

  const refundOrPayable = v(c?.refundOrPayable);

  const go = (row: Row) => {
    if (row.href) router.push(row.href);
    else if (row.scrollTo) document.getElementById(row.scrollTo)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="flex items-center gap-2">
            <Calculator className="h-5 w-5 text-brand-600" aria-hidden="true" />
            Computation
          </CardTitle>
          <div className="flex items-center gap-2">
            <Badge tone={regime === 'New' ? 'brand' : 'neutral'}>{regime} regime</Badge>
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.push(`/returns/${returnId}/file/regime`)}
            >
              <Scale className="h-3.5 w-3.5" aria-hidden="true" />
              Compare regimes
            </Button>
          </div>
        </div>
        <CardDescription>
          Click any income, deduction or prepaid-tax line to jump straight to the form that fills it.
          Tax, surcharge, cess and interest are computed automatically.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {computeQuery.isLoading ? (
          <div className="flex justify-center py-6">
            <Spinner label="Computing…" />
          </div>
        ) : (
          <>
            <ul className="divide-y divide-ink-100">
              {rows.map((row) => {
                const clickable = !!row.href || !!row.scrollTo;
                const RowTag = clickable ? 'button' : 'div';
                return (
                  <li key={row.key}>
                    <RowTag
                      type={clickable ? 'button' : undefined}
                      onClick={clickable ? () => go(row) : undefined}
                      className={cn(
                        'flex w-full items-center justify-between gap-3 px-1 py-2 text-left text-sm',
                        clickable && 'group hover:bg-brand-50/60 rounded-lg cursor-pointer',
                        row.kind === 'subtotal' && 'bg-ink-50 font-semibold text-ink-900 rounded-lg px-2',
                        row.indent && 'pl-5',
                      )}
                    >
                      <span
                        className={cn(
                          'flex items-center gap-1.5',
                          row.kind === 'subtotal' ? 'text-ink-900' : 'text-ink-700',
                          row.kind === 'head' && 'font-medium',
                        )}
                      >
                        {row.label}
                        {clickable && (
                          <ChevronRight className="h-3.5 w-3.5 text-ink-300 transition-transform group-hover:translate-x-0.5 group-hover:text-brand-500" aria-hidden="true" />
                        )}
                      </span>
                      <span
                        className={cn(
                          'shrink-0 tabular-nums',
                          row.kind === 'subtotal' ? 'font-semibold text-ink-900' : 'text-ink-700',
                          row.amount === 0 && 'text-ink-300',
                        )}
                      >
                        {row.amount === 0 ? '—' : formatInr(row.amount)}
                      </span>
                    </RowTag>
                  </li>
                );
              })}
            </ul>

            {carried.length > 0 && (
              <div className="mt-3 rounded-xl border border-ink-100 p-3">
                <p className="mb-1 text-xs font-medium uppercase tracking-wide text-ink-400">Carried forward to next year</p>
                <ul className="space-y-0.5">
                  {carried.map((r) => (
                    <li key={r.key} className="flex items-center justify-between text-xs text-ink-500">
                      <span>{r.label}</span>
                      <span className="tabular-nums">{formatInr(r.amount)}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Net result */}
            <div
              className={cn(
                'mt-3 flex items-center justify-between gap-3 rounded-xl px-4 py-3',
                refundOrPayable >= 0 ? 'bg-money-50' : 'bg-payable-50',
              )}
            >
              <span className="text-sm font-medium text-ink-700">
                {refundOrPayable >= 0 ? 'Refund due' : 'Tax payable'}
              </span>
              <span
                className={cn(
                  'text-xl font-bold tabular-nums',
                  refundOrPayable >= 0 ? 'text-money-700' : 'text-payable-700',
                )}
              >
                {formatInr(Math.abs(refundOrPayable))}
              </span>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
