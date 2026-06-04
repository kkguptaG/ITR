'use client';

// ---------------------------------------------------------------------------
// /returns/[returnId]/workspace — the Computation Workspace: a header (assessee /
// PAN / AY / ITR + validate/recalculate/print), a contextual section nav, the
// per-head computation, and a summary/insights/alerts rail. Driven by the live
// tax-compute result + the return's income heads + the pre-file validation.
// ---------------------------------------------------------------------------

import { useQuery } from '@tanstack/react-query';
import {
  Briefcase,
  Home,
  LineChart,
  PiggyBank,
  Printer,
  RefreshCw,
  Save,
  ShieldCheck,
  Wallet,
} from 'lucide-react';
import { Button, Spinner, Alert } from '@/components/ui';
import { useAuth } from '@/lib/auth';
import { formatAssessmentYear } from '@/lib/format';
import { formatItrType } from '@/features/returns/helpers';
import {
  computeTax,
  validateReturn,
  getReturn,
  listSalaries,
  listHouseProperties,
  listCapitalGains,
  listBusinessIncomes,
  listIncomeSources,
  filingKeys,
} from '@/features/filing/api';
import { incomeHeads } from '@/features/filing/steps';
import { WorkspaceNav } from '@/features/filing/components/workspace/WorkspaceNav';
import { WorkspaceComputation, type WorkspaceSection } from '@/features/filing/components/workspace/WorkspaceComputation';
import { WorkspaceRail } from '@/features/filing/components/workspace/WorkspaceRail';

export default function WorkspacePage({ params }: { params: { returnId: string } }) {
  const { returnId } = params;
  const { user } = useAuth();

  const detailQuery = useQuery({ queryKey: filingKeys.detail(returnId), queryFn: () => getReturn(returnId) });
  const computeQuery = useQuery({ queryKey: filingKeys.compute(returnId), queryFn: () => computeTax({ returnId }), retry: false });
  const validateQuery = useQuery({ queryKey: ['filing', 'validate', returnId], queryFn: () => validateReturn(returnId), retry: false });
  const salariesQuery = useQuery({ queryKey: filingKeys.salaries(returnId), queryFn: () => listSalaries(returnId) });
  const housesQuery = useQuery({ queryKey: filingKeys.houses(returnId), queryFn: () => listHouseProperties(returnId) });
  const cgQuery = useQuery({ queryKey: filingKeys.gains(returnId), queryFn: () => listCapitalGains(returnId) });
  const bizQuery = useQuery({ queryKey: filingKeys.business(returnId), queryFn: () => listBusinessIncomes(returnId) });
  const otherQuery = useQuery({ queryKey: filingKeys.incomeSources(returnId), queryFn: () => listIncomeSources(returnId) });

  const detail = detailQuery.data;
  const compute = computeQuery.data;
  const regime = detail?.regime ?? compute?.recommendedRegime ?? 'New';
  const c = compute ? (regime === 'Old' ? compute.old : compute.new) : null;

  if (detailQuery.isLoading || (computeQuery.isLoading && !c)) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <Spinner size={28} label="Loading computation workspace…" />
      </div>
    );
  }
  if (!detail) return <Alert variant="error">We couldn&apos;t load this return.</Alert>;
  if (!c) {
    return <Alert variant="warning">Add some income to compute this return, then come back to the workspace.</Alert>;
  }

  const heads = incomeHeads(detail.itrType ?? null);
  const hpType = (t: string) => (t === 'SelfOccupied' ? 'Self-occupied' : t === 'LetOut' ? 'Let-out' : t === 'DeemedLetOut' ? 'Deemed let-out' : t);

  const defs = [
    { id: 'salary', letter: 'A', title: 'Income from Salary', icon: Briefcase, applies: heads.salary, net: c.salaryNetIncome,
      entries: (salariesQuery.data ?? []).map((s) => ({ label: s.employer || 'Salary', amount: s.gross, note: s.tan ? `Form 16 · ${s.tan}` : 'Form 16' })) },
    { id: 'house', letter: 'B', title: 'Income from House Property', icon: Home, applies: heads.houseProperty, net: c.housePropertyNetIncome,
      entries: (housesQuery.data ?? []).map((h) => ({ label: hpType(h.type), amount: h.annualRent || h.annualValue, note: h.address })) },
    { id: 'business', letter: 'C', title: 'Profits & Gains of Business / Profession', icon: Wallet, applies: heads.business, net: c.businessNetIncome,
      entries: (bizQuery.data ?? []).map((b) => ({ label: b.natureOfBusinessCode || 'Business income', amount: b.turnover, note: b.isPresumptive ? `Presumptive ${b.presumptiveSection ?? ''}`.trim() : 'Regular books' })) },
    { id: 'cg', letter: 'D', title: 'Capital Gains', icon: LineChart, applies: heads.capitalGains, net: c.capitalGainsNetIncome,
      entries: (cgQuery.data ?? []).map((g) => ({ label: `${g.term === 'Long' ? 'Long-term' : 'Short-term'} · ${g.assetType}`, amount: g.salePrice, note: g.taxSection })) },
    { id: 'other', letter: 'E', title: 'Income from Other Sources', icon: PiggyBank, applies: heads.otherSources, net: c.otherSourcesNetIncome,
      entries: (otherQuery.data ?? []).map((o) => ({ label: o.label || o.type, amount: o.amount, note: o.type })) },
  ];
  const sections: WorkspaceSection[] = defs
    .filter((s) => s.applies || s.net !== 0 || s.entries.length > 0)
    .map(({ applies: _a, ...s }) => s);
  const incomeSections = sections.map((s) => ({ id: s.id, label: s.title.replace(/^Income from /, '').replace('Profits & Gains of ', '') }));

  const insights: string[] = [];
  if (regime === 'Old' && c.totalDeductions < 150_000) insights.push('Room to claim more under Section 80C / 80D — review your deductions.');
  if (c.refundOrPayable > 0) insights.push(`A refund of about ₹${Math.round(c.refundOrPayable).toLocaleString('en-IN')} is expected.`);
  else if (c.refundOrPayable < 0) insights.push(`A balance tax of ₹${Math.round(-c.refundOrPayable).toLocaleString('en-IN')} is payable before filing.`);
  if (c.interestPenalty > 0) insights.push('Interest u/s 234A/B/C applies — file/pay sooner to minimise it.');
  if (insights.length === 0) insights.push('Your computation looks complete. Review and file before the due date.');

  const findings = validateQuery.data?.findings ?? [];

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col gap-4 rounded-2xl border border-ink-200 bg-white p-4 shadow-card xl:flex-row xl:items-center xl:justify-between">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <div>
            <h1 className="text-lg font-semibold text-ink-900">Computation Workspace</h1>
            <p className="text-xs text-ink-500">Tax calculated as per the {regime} regime</p>
          </div>
          <Field label="Assessee" value={user?.fullName ?? '—'} />
          <Field label="PAN" value={user?.panMasked ?? '—'} mono />
          <Field label="Assessment Year" value={formatAssessmentYear(detail.assessmentYear)} />
          <Field label="ITR Type" value={detail.itrType ? formatItrType(detail.itrType) : '—'} />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => void validateQuery.refetch()} loading={validateQuery.isFetching}>
            <ShieldCheck className="h-4 w-4" aria-hidden="true" /> Validate
          </Button>
          <Button variant="outline" size="sm" onClick={() => void computeQuery.refetch()} loading={computeQuery.isFetching}>
            <RefreshCw className="h-4 w-4" aria-hidden="true" /> Recalculate
          </Button>
          <Button variant="ghost" size="sm" disabled>
            <Save className="h-4 w-4" aria-hidden="true" /> Saved
          </Button>
          <Button variant="ghost" size="sm" onClick={() => window.print()}>
            <Printer className="h-4 w-4" aria-hidden="true" /> Print
          </Button>
        </div>
      </div>

      {/* 3-pane */}
      <div className="grid gap-6 lg:grid-cols-[200px_minmax(0,1fr)] xl:grid-cols-[200px_minmax(0,1fr)_320px]">
        <aside className="hidden lg:block">
          <div className="sticky top-4 rounded-2xl border border-ink-200 bg-white p-2 shadow-card">
            <WorkspaceNav returnId={returnId} incomeSections={incomeSections} />
          </div>
        </aside>

        <main className="min-w-0">
          <WorkspaceComputation
            sections={sections}
            gti={c.grossTotalIncome}
            totalDeductions={c.totalDeductions}
            taxableIncome={c.taxableIncome}
            taxPayable={c.totalTax}
            regime={regime}
          />
          <div id="ws-summary" className="scroll-mt-24" />
        </main>

        <aside className="hidden xl:block">
          <div className="sticky top-4">
            <WorkspaceRail result={c} regime={regime} findings={findings} insights={insights} />
          </div>
        </aside>
      </div>

      {/* Shortcut bar */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-xl border border-ink-200 bg-white px-4 py-2 text-xs text-ink-400">
        <span><b className="text-ink-600">Validate</b> checks before filing</span>
        <span><b className="text-ink-600">Recalculate</b> refreshes the tax</span>
        <span><b className="text-ink-600">Print</b> the computation</span>
        <span className="ml-auto">Auto-saved · live computation</span>
      </div>
    </div>
  );
}

function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <p className="text-[11px] font-medium uppercase tracking-wide text-ink-400">{label}</p>
      <p className={`text-sm font-semibold text-ink-900 ${mono ? 'font-mono' : ''}`}>{value}</p>
    </div>
  );
}
