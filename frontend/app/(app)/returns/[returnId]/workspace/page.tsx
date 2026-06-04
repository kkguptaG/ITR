'use client';

// ---------------------------------------------------------------------------
// /returns/[returnId]/workspace — the Computation Workspace: a header (assessee /
// PAN / AY / ITR + validate/recalculate/print), a contextual section nav, the
// per-head computation, and a summary/insights/alerts rail. Driven by the live
// tax-compute result + the return's income heads + the pre-file validation.
// ---------------------------------------------------------------------------

import { useQuery } from '@tanstack/react-query';
import { useTranslations } from 'next-intl';
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
import { formatAssessmentYear, formatInr } from '@/lib/format';
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
  const t = useTranslations('workspace');

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
        <Spinner size={28} label={t('loading')} />
      </div>
    );
  }
  if (!detail) return <Alert variant="error">{t('loadError')}</Alert>;
  if (!c) {
    return <Alert variant="warning">{t('needIncome')}</Alert>;
  }

  const heads = incomeHeads(detail.itrType ?? null);
  const hpType = (v: string) => (v === 'SelfOccupied' ? 'Self-occupied' : v === 'LetOut' ? 'Let-out' : v === 'DeemedLetOut' ? 'Deemed let-out' : v);

  const defs = [
    { id: 'salary', letter: 'A', title: t('headSalary'), short: t('headSalary'), icon: Briefcase, applies: heads.salary, net: c.salaryNetIncome,
      entries: (salariesQuery.data ?? []).map((s) => ({ label: s.employer || 'Salary', amount: s.gross, note: s.tan ? `Form 16 · ${s.tan}` : 'Form 16' })) },
    { id: 'house', letter: 'B', title: t('headHouse'), short: t('headHouse'), icon: Home, applies: heads.houseProperty, net: c.housePropertyNetIncome,
      entries: (housesQuery.data ?? []).map((h) => ({ label: hpType(h.type), amount: h.annualRent || h.annualValue, note: h.address })) },
    { id: 'business', letter: 'C', title: t('headBusiness'), short: t('headBusiness'), icon: Wallet, applies: heads.business, net: c.businessNetIncome,
      entries: (bizQuery.data ?? []).map((b) => ({ label: b.natureOfBusinessCode || 'Business income', amount: b.turnover, note: b.isPresumptive ? `Presumptive ${b.presumptiveSection ?? ''}`.trim() : 'Regular books' })) },
    { id: 'cg', letter: 'D', title: t('headCg'), short: t('headCg'), icon: LineChart, applies: heads.capitalGains, net: c.capitalGainsNetIncome,
      entries: (cgQuery.data ?? []).map((g) => ({ label: `${g.term === 'Long' ? 'Long-term' : 'Short-term'} · ${g.assetType}`, amount: g.salePrice, note: g.taxSection })) },
    { id: 'other', letter: 'E', title: t('headOther'), short: t('headOther'), icon: PiggyBank, applies: heads.otherSources, net: c.otherSourcesNetIncome,
      entries: (otherQuery.data ?? []).map((o) => ({ label: o.label || o.type, amount: o.amount, note: o.type })) },
  ];
  const sections: WorkspaceSection[] = defs
    .filter((s) => s.applies || s.net !== 0 || s.entries.length > 0)
    .map(({ applies: _a, short: _s, ...s }) => s);
  const incomeSections = defs
    .filter((s) => s.applies || s.net !== 0 || s.entries.length > 0)
    .map((s) => ({ id: s.id, label: s.short }));

  const insights: string[] = [];
  if (regime === 'Old' && c.totalDeductions < 150_000) insights.push(t('insightDeductions'));
  if (c.refundOrPayable > 0) insights.push(t('insightRefund', { amount: formatInr(c.refundOrPayable) }));
  else if (c.refundOrPayable < 0) insights.push(t('insightPayable', { amount: formatInr(-c.refundOrPayable) }));
  if (c.interestPenalty > 0) insights.push(t('insightInterest'));
  if (insights.length === 0) insights.push(t('insightComplete'));

  const findings = validateQuery.data?.findings ?? [];

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col gap-4 rounded-2xl border border-ink-200 bg-white p-4 shadow-card xl:flex-row xl:items-center xl:justify-between">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <div>
            <h1 className="text-lg font-semibold text-ink-900">{t('title')}</h1>
            <p className="text-xs text-ink-500">{t('regimeLine', { regime })}</p>
          </div>
          <Field label={t('assessee')} value={user?.fullName ?? '—'} />
          <Field label={t('pan')} value={user?.panMasked ?? '—'} mono />
          <Field label={t('assessmentYear')} value={formatAssessmentYear(detail.assessmentYear)} />
          <Field label={t('itrType')} value={detail.itrType ? formatItrType(detail.itrType) : '—'} />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => void validateQuery.refetch()} loading={validateQuery.isFetching}>
            <ShieldCheck className="h-4 w-4" aria-hidden="true" /> {t('validate')}
          </Button>
          <Button variant="outline" size="sm" onClick={() => void computeQuery.refetch()} loading={computeQuery.isFetching}>
            <RefreshCw className="h-4 w-4" aria-hidden="true" /> {t('recalculate')}
          </Button>
          <Button variant="ghost" size="sm" disabled>
            <Save className="h-4 w-4" aria-hidden="true" /> {t('saved')}
          </Button>
          <Button variant="ghost" size="sm" onClick={() => window.print()}>
            <Printer className="h-4 w-4" aria-hidden="true" /> {t('print')}
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
        <span><b className="text-ink-600">{t('validate')}</b> {t('scValidate')}</span>
        <span><b className="text-ink-600">{t('recalculate')}</b> {t('scRecalc')}</span>
        <span><b className="text-ink-600">{t('print')}</b> {t('scPrint')}</span>
        <span className="ml-auto">{t('autoSaved')}</span>
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
