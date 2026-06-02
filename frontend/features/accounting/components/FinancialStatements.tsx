'use client';

// ---------------------------------------------------------------------------
// FinancialStatements — Balance Sheet + Profit & Loss derived from the user's
// double-entry books (GET /accounting/financial-statements). Read-only; the
// same figures feed ITR-3's PARTA_BS / PARTA_PL.
// ---------------------------------------------------------------------------

import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, AlertTriangle } from 'lucide-react';
import { Alert, Card, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatInr } from '@/lib/format';
import { accountingKeys, getFinancialStatements } from '../api';
import type { GroupBalanceDto } from '../types';

// "SalesIncome" → "Sales income"; "NetProfitToCapital" → "Net profit to capital".
function humanize(group: string): string {
  const spaced = group.replace(/([a-z])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

function Section({ title, rows, total, totalLabel }: { title: string; rows: GroupBalanceDto[]; total: number; totalLabel: string }) {
  return (
    <div>
      <div className="bg-ink-50 px-3 py-1.5 text-xs font-semibold uppercase tracking-wide text-ink-500">{title}</div>
      <table className="w-full border-collapse text-sm">
        <tbody>
          {rows.length === 0 ? (
            <tr><td className="px-3 py-2 text-ink-400">—</td></tr>
          ) : (
            rows.map((r) => (
              <tr key={r.group} className="border-t border-ink-100">
                <td className="px-3 py-2 text-ink-700">{humanize(r.group)}</td>
                <td className="px-3 py-2 text-right tabular-nums text-ink-900">{formatInr(r.amount)}</td>
              </tr>
            ))
          )}
          <tr className="border-t border-ink-200 font-semibold">
            <td className="px-3 py-2 text-ink-900">{totalLabel}</td>
            <td className="px-3 py-2 text-right tabular-nums text-ink-900">{formatInr(total)}</td>
          </tr>
        </tbody>
      </table>
    </div>
  );
}

export function FinancialStatements() {
  const query = useQuery({
    queryKey: accountingKeys.financialStatements(),
    queryFn: getFinancialStatements,
  });

  if (query.isLoading) return <Spinner label="Deriving statements from your books…" />;
  if (query.isError) {
    return (
      <Alert variant="error" title="Couldn't load financial statements">
        {query.error instanceof ApiError ? (query.error.problem.detail ?? query.error.message) : 'Please try again.'}
      </Alert>
    );
  }

  const { profitAndLoss: pl, balanceSheet: bs } = query.data!;

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card className="overflow-hidden">
        <div className="border-b border-ink-100 bg-brand-50/60 px-5 py-3">
          <h2 className="font-semibold text-ink-900">Profit &amp; Loss</h2>
        </div>
        <div className="divide-y divide-ink-100">
          <Section title="Income" rows={pl.income} total={pl.totalIncome} totalLabel="Total income" />
          <Section title="Expenses" rows={pl.expenses} total={pl.totalExpenses} totalLabel="Total expenses" />
          <div className={pl.netProfit >= 0 ? 'flex items-center justify-between px-3 py-3 bg-money-50' : 'flex items-center justify-between px-3 py-3 bg-payable-50'}>
            <span className="text-sm font-semibold text-ink-800">{pl.netProfit >= 0 ? 'Net profit' : 'Net loss'}</span>
            <span className="tabular-nums text-base font-semibold text-ink-900">{formatInr(Math.abs(pl.netProfit))}</span>
          </div>
        </div>
      </Card>

      <Card className="overflow-hidden">
        <div className="flex items-center justify-between border-b border-ink-100 bg-brand-50/60 px-5 py-3">
          <h2 className="font-semibold text-ink-900">Balance Sheet</h2>
          {bs.isBalanced ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-money-50 px-2 py-0.5 text-xs font-semibold text-money-700">
              <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" /> Balanced
            </span>
          ) : (
            <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-700">
              <AlertTriangle className="h-3.5 w-3.5" aria-hidden="true" /> Out of balance
            </span>
          )}
        </div>
        <div className="divide-y divide-ink-100">
          <Section title="Assets" rows={bs.assets} total={bs.totalAssets} totalLabel="Total assets" />
          <Section title="Liabilities &amp; capital" rows={bs.liabilitiesAndCapital} total={bs.totalLiabilitiesAndCapital} totalLabel="Total liabilities & capital" />
        </div>
      </Card>
    </div>
  );
}
