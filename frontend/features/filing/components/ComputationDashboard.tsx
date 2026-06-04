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
import { ChevronRight, Calculator, Scale, Plus, Landmark, CheckCircle2, AlertTriangle, Info } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, Spinner, Badge, Button } from '@/components/ui';
import { formatInr } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { ItrType } from '@/lib/api-types';
import { listBankAccounts, bankAccountKeys } from '@/features/bank-accounts';
import { computeTax, validateReturn, filingKeys } from '../api';
import { incomeHeads } from '../steps';
import type { Regime, TaxComputationResultDto } from '../types';

type RowKind = 'head' | 'subtotal' | 'deduction' | 'tax' | 'prepaid' | 'result' | 'minor' | 'subline';

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
  detail: { regime: Regime | null; itrType: ItrType | null };
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

  // A refund can only be paid into a pre-validated bank account, so when one is
  // due we check whether the assessee has nominated a refund account and prompt
  // for it if not. Only fetched once a refund is actually computed.
  const refundDue = (c?.refundOrPayable ?? 0) > 0;
  const bankAccountsQuery = useQuery({
    queryKey: bankAccountKeys.list(),
    queryFn: listBankAccounts,
    enabled: refundDue,
    staleTime: 30_000,
  });
  const needsRefundAccount =
    refundDue &&
    bankAccountsQuery.isSuccess &&
    !(bankAccountsQuery.data ?? []).some((a) => a.useForRefund);

  // Pre-filing validation — the same ITD defect checks the Summary/File steps run
  // (shared query key), surfaced on the hub so filing-readiness shows at a glance.
  const validateQuery = useQuery({
    queryKey: [...filingKeys.compute(returnId), 'validate'],
    queryFn: () => validateReturn(returnId),
    retry: false,
    staleTime: 10_000,
  });
  const findings = validateQuery.data?.findings ?? [];
  const blockerCount = findings.filter((f) => f.severity === 'block').length;
  // Non-blocking findings (warn + info) are grouped as "worth a look", matching
  // how the Summary step buckets them, so the hub and the step always agree.
  const warningCount = findings.filter((f) => f.severity !== 'block').length;

  const incomeHref = (focus: string) => `/returns/${returnId}/file/income?focus=${focus}`;
  const reviewHref = `/returns/${returnId}/file/summary`;

  // Build the computation rows from the chosen-regime result (zeros when not yet computed).
  const v = (n: number | undefined) => n ?? 0;
  // Show only the income heads relevant to this ITR form — but never hide a head
  // that already carries income (a positive gain or a loss), so nothing the user
  // entered is ever silently dropped from the computation.
  const heads = incomeHeads(detail.itrType);
  const showHead = (relevant: boolean, amount: number) => relevant || amount !== 0;

  // Rate-wise capital-gains + casual sub-lines (ITD Schedule SI), itemised under their head so the
  // dashboard mirrors a CA-grade computation sheet. Each appears only when its bucket is non-zero.
  const si = c?.specialIncome;
  const sub = (key: string, label: string, amount: number): Row[] =>
    amount !== 0 ? [{ key, label, amount, kind: 'subline' as const, indent: true }] : [];
  const cgSubLines: Row[] = si
    ? [
        ...sub('cg-slab', 'STCG, other assets (slab rate)', v(si.slabRateCapitalGains)),
        ...sub('cg-111a', 'STCG, listed equity (s.111A)', v(si.stcg111A)),
        ...sub('cg-112a', 'LTCG, listed equity (s.112A)', v(si.ltcg112A)),
        ...sub('cg-112', 'LTCG, other assets (s.112)', v(si.ltcg112)),
        ...sub('cg-vda', 'VDA / crypto (s.115BBH)', v(si.vda115BBH)),
      ]
    : [];
  const winningsSubLine: Row[] = sub('os-115bb', 'Winnings / lottery (s.115BB)', v(si?.casual115BB));

  const rows: Row[] = [
    ...(showHead(heads.salary, v(c?.salaryNetIncome)) ? [{ key: 'salary', label: 'Salary', amount: v(c?.salaryNetIncome), kind: 'head' as const, href: incomeHref('salary') }] : []),
    ...(showHead(heads.business, v(c?.businessNetIncome)) ? [{ key: 'business', label: 'Income from Business or Profession', amount: v(c?.businessNetIncome), kind: 'head' as const, href: incomeHref('business') }] : []),
    ...(showHead(heads.houseProperty, v(c?.housePropertyNetIncome)) ? [{ key: 'house', label: 'Income from House Property', amount: v(c?.housePropertyNetIncome), kind: 'head' as const, href: incomeHref('house') }] : []),
    ...(showHead(heads.capitalGains, v(c?.capitalGainsNetIncome)) ? [{ key: 'cg', label: 'Capital Gains', amount: v(c?.capitalGainsNetIncome), kind: 'head' as const, href: incomeHref('capitalGains') }, ...cgSubLines] : []),
    ...(showHead(heads.otherSources, v(c?.otherSourcesNetIncome)) ? [{ key: 'other', label: 'Income from Other Sources', amount: v(c?.otherSourcesNetIncome), kind: 'head' as const, href: incomeHref('other') }, ...winningsSubLine] : []),
    { key: 'gti', label: 'Gross Total Income', amount: v(c?.grossTotalIncome), kind: 'subtotal' as const },
    { key: 'via', label: 'Less: Deductions under Chapter VI-A', amount: v(c?.totalDeductions), kind: 'deduction' as const, href: `/returns/${returnId}/file/deductions` },
    { key: 'taxable', label: 'Taxable Income', amount: v(c?.taxableIncome), kind: 'subtotal' as const },
    { key: 'taxbefore', label: 'Income Tax', amount: v(c?.taxBeforeRebate), kind: 'tax' as const },
    // Split the tax into its slab-rate vs special-rate components — only meaningful when some income
    // is charged at special rates (capital gains / winnings).
    ...(v(c?.taxAtSpecialRates) > 0
      ? [
          { key: 'tax-normal', label: 'on income at normal (slab) rates', amount: v(c?.taxAtNormalRates), kind: 'subline' as const, indent: true },
          { key: 'tax-special', label: 'on income at special rates', amount: v(c?.taxAtSpecialRates), kind: 'subline' as const, indent: true },
        ]
      : []),
    ...(v(c?.rebate87A) > 0 ? [{ key: 'r87a', label: 'Less: Rebate u/s 87A', amount: v(c?.rebate87A), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.surcharge) > 0 ? [{ key: 'sur', label: 'Add: Surcharge', amount: v(c?.surcharge), kind: 'tax' as const, indent: true }] : []),
    { key: 'cess', label: 'Add: Health & Education Cess', amount: v(c?.cess), kind: 'tax' as const, indent: true },
    ...(v(c?.alternativeMinimumTax) > 0 ? [{ key: 'amt', label: 'Alternate Minimum Tax (s.115JC)', amount: v(c?.alternativeMinimumTax), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.relief89) > 0 ? [{ key: 'rel89', label: 'Less: Relief u/s 89 (arrears)', amount: v(c?.relief89), kind: 'tax' as const, indent: true }] : []),
    ...(v(c?.relief90And91) > 0 ? [{ key: 'rel90', label: 'Less: Relief u/s 90/91 (foreign tax)', amount: v(c?.relief90And91), kind: 'tax' as const, indent: true }] : []),
    { key: 'totaltax', label: 'Total Tax Liability', amount: v(c?.totalTax), kind: 'subtotal' as const },
    // Interest u/s 234A/B/C is a default add-on, not part of the statutory tax
    // liability — show it *after* the Total Tax Liability subtotal so the running
    // tally (subtotal + interest − prepaid credits = payable) reads top-to-bottom.
    ...(v(c?.interestPenalty) > 0 ? [{ key: 'int', label: 'Add: Interest u/s 234A/B/C', amount: v(c?.interestPenalty), kind: 'tax' as const }] : []),
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
            {/* Filing-readiness at a glance — surfaces the ITD defect checks on the
                hub and routes to the Summary step where each finding is detailed. */}
            {validateQuery.isSuccess && (
              <button
                type="button"
                onClick={() => router.push(reviewHref)}
                className={cn(
                  'mb-3 flex w-full items-center justify-between gap-2 rounded-xl border px-3 py-2 text-left text-sm transition-colors',
                  blockerCount > 0
                    ? 'border-red-200 bg-red-50 text-red-900 hover:bg-red-100'
                    : warningCount > 0
                      ? 'border-payable-200 bg-payable-50 text-payable-800 hover:bg-payable-100'
                      : 'border-money-200 bg-money-50 text-money-800 hover:bg-money-100',
                )}
              >
                <span className="flex items-center gap-2">
                  {blockerCount > 0 ? (
                    <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
                  ) : warningCount > 0 ? (
                    <Info className="h-4 w-4 shrink-0" aria-hidden="true" />
                  ) : (
                    <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden="true" />
                  )}
                  <span className="font-medium">
                    {blockerCount > 0
                      ? `${blockerCount} ${blockerCount === 1 ? 'issue' : 'issues'} to fix before you can file`
                      : warningCount > 0
                        ? `${warningCount} ${warningCount === 1 ? 'thing' : 'things'} worth reviewing before filing`
                        : 'No issues found — ready to review and file'}
                  </span>
                </span>
                <ChevronRight className="h-4 w-4 shrink-0 opacity-60" aria-hidden="true" />
              </button>
            )}
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
                        row.kind === 'subline' && 'py-1 text-xs',
                        row.indent && 'pl-5',
                      )}
                    >
                      <span
                        className={cn(
                          'flex items-center gap-1.5',
                          row.kind === 'subtotal'
                            ? 'text-ink-900'
                            : row.kind === 'subline'
                              ? 'text-ink-400'
                              : 'text-ink-700',
                          row.kind === 'head' && 'font-medium',
                        )}
                      >
                        {row.label}
                        {clickable && (
                          <ChevronRight className="h-3.5 w-3.5 text-ink-300 transition-transform group-hover:translate-x-0.5 group-hover:text-brand-500" aria-hidden="true" />
                        )}
                      </span>
                      {row.amount === 0 && clickable && (row.kind === 'head' || row.kind === 'deduction') ? (
                        // Empty but fillable — invite the user to add it rather than
                        // showing a dead dash, so they can see at a glance what is
                        // still blank ("nothing is left out").
                        <span className="inline-flex shrink-0 items-center gap-0.5 text-sm font-medium text-brand-600 group-hover:text-brand-700">
                          Add
                          <Plus className="h-3.5 w-3.5" aria-hidden="true" />
                        </span>
                      ) : (
                        <span
                          className={cn(
                            'shrink-0 tabular-nums',
                            row.kind === 'subtotal'
                              ? 'font-semibold text-ink-900'
                              : row.kind === 'subline'
                                ? 'text-ink-400'
                                : 'text-ink-700',
                            row.amount === 0 && 'text-ink-300',
                          )}
                        >
                          {row.amount === 0 ? '—' : formatInr(row.amount)}
                        </span>
                      )}
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

            {/* A refund needs a pre-validated bank account — nudge the user to add
                one so the refund isn't held up. Scrolls to the BankAccountsCard. */}
            {needsRefundAccount && (
              <button
                type="button"
                onClick={() =>
                  document.getElementById('bank-accounts')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
                }
                className="group mt-2 flex w-full items-center justify-between gap-2 rounded-lg border border-money-200 bg-money-50/50 px-3 py-2 text-left text-xs text-money-800 hover:bg-money-100"
              >
                <span className="flex items-center gap-1.5">
                  <Landmark className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                  Add a bank account to receive your refund — the Income Tax Department only pays into a pre-validated account.
                </span>
                <ChevronRight
                  className="h-3.5 w-3.5 shrink-0 text-money-400 transition-transform group-hover:translate-x-0.5"
                  aria-hidden="true"
                />
              </button>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
