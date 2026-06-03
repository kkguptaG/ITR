'use client';

// ---------------------------------------------------------------------------
// BusinessIncomeSummaryCard — a read-only review of the business/profession
// head for ITR-3 and ITR-4. Shows each business's presumptive section (or
// regular books), turnover, the computed presumptive / net profit, the 44AE
// vehicle count, and the no-account-case financial particulars when present.
// Rendered in ReturnDetailView; editing happens in the wizard's Income step.
// ---------------------------------------------------------------------------

import { useQuery } from '@tanstack/react-query';
import { Briefcase } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, Spinner, Badge } from '@/components/ui';
import { formatInr } from '@/lib/format';
import { filingKeys, listBusinessIncomes } from '../api';
import type { BusinessIncomeDto } from '../types';

const SECTION_LABEL: Record<string, string> = {
  '44AD': '44AD — presumptive business',
  '44ADA': '44ADA — presumptive profession',
  '44AE': '44AE — goods carriage',
};

function vehicleCount(json: string | null | undefined): number {
  if (!json) return 0;
  try {
    const a = JSON.parse(json);
    return Array.isArray(a) ? a.length : 0;
  } catch {
    return 0;
  }
}

/** Sum of the financial-particulars liability side (capital + loans + creditors). */
function liabilitiesTotal(b: BusinessIncomeDto): number {
  return b.partnerCapital + b.securedLoans + b.unsecuredLoans + b.sundryCreditors;
}
function assetsTotal(b: BusinessIncomeDto): number {
  return b.fixedAssets + b.inventory + b.sundryDebtors + b.bankBalance + b.cashBalance;
}

export function BusinessIncomeSummaryCard({ returnId }: { returnId: string }) {
  const query = useQuery({
    queryKey: filingKeys.business(returnId),
    queryFn: () => listBusinessIncomes(returnId),
    staleTime: 10_000,
  });

  const items = query.data ?? [];
  if (!query.isLoading && items.length === 0) {
    return null; // no business income → don't render the card
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Briefcase className="h-5 w-5 text-brand-600" aria-hidden="true" />
          Business / profession
        </CardTitle>
        <CardDescription>
          The business head as it will appear in Schedule BP. Edit it in the Income step of the wizard.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {query.isLoading ? (
          <div className="flex justify-center py-4">
            <Spinner />
          </div>
        ) : (
          <ul className="space-y-3">
            {items.map((b) => {
              const vehicles = vehicleCount(b.goodsCarriageJson);
              const liab = liabilitiesTotal(b);
              const assets = assetsTotal(b);
              const hasParticulars = liab > 0 || assets > 0;
              return (
                <li key={b.id} className="rounded-xl border border-ink-200 p-3.5">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      {b.isPresumptive ? (
                        <Badge tone="brand">{SECTION_LABEL[b.presumptiveSection ?? '44AD'] ?? b.presumptiveSection}</Badge>
                      ) : (
                        <Badge tone="neutral">Regular books</Badge>
                      )}
                      {b.speculativeFlag && <Badge tone="warning">Speculative</Badge>}
                      {b.natureOfBusinessCode && (
                        <span className="text-xs text-ink-400">code {b.natureOfBusinessCode}</span>
                      )}
                    </div>
                    <span className="text-sm font-semibold tabular-nums text-ink-900">
                      {formatInr(b.isPresumptive && b.presumptiveSection !== '44AE'
                        ? Math.round(b.netProfit)
                        : b.netProfit)}
                    </span>
                  </div>

                  <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-sm sm:grid-cols-3">
                    {b.presumptiveSection !== '44AE' && (
                      <Row label="Turnover / receipts" value={b.turnover} />
                    )}
                    {b.isPresumptive && b.presumptiveSection === '44AD' && (
                      <>
                        <Row label="Digital (6%)" value={b.grossReceiptsDigital} />
                        <Row label="Cash (8%)" value={b.grossReceiptsCash} />
                      </>
                    )}
                    {b.presumptiveSection === '44AE' && (
                      <div className="text-ink-600">
                        <span className="text-ink-400">Vehicles:</span>{' '}
                        <span className="font-medium tabular-nums">{vehicles}</span>
                      </div>
                    )}
                    <Row label={b.isPresumptive ? 'Presumptive income' : 'Net profit'} value={b.netProfit} strong />
                  </dl>

                  {hasParticulars && (
                    <div className="mt-2 border-t border-ink-100 pt-2 text-xs text-ink-500">
                      Financial particulars: capital + liabilities {formatInr(liab)} · assets {formatInr(assets)}
                      {Math.abs(liab - assets) > 0.1 * Math.max(liab, assets) && liab > 0 && assets > 0 && (
                        <span className="ml-1 text-amber-600">(doesn&apos;t balance)</span>
                      )}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function Row({ label, value, strong }: { label: string; value: number; strong?: boolean }) {
  return (
    <div className={strong ? 'font-medium text-ink-800' : 'text-ink-600'}>
      <span className="text-ink-400">{label}:</span>{' '}
      <span className="tabular-nums">{formatInr(value)}</span>
    </div>
  );
}
