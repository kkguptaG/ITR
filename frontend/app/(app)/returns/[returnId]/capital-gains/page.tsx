'use client';

// ---------------------------------------------------------------------------
// /returns/[returnId]/capital-gains — the Capital Gains Hub (next-gen CG module,
// Layer 4). A summary dashboard + 8 asset-category cards + a Beginner/Pro toggle,
// all driven by the live capital-gain rows and the tax-compute result. Adding /
// editing reuses the asset-driven CapitalGainForm. (P2 of docs/architecture/11.)
// ---------------------------------------------------------------------------

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslations } from 'next-intl';
import { ArrowLeft, Plus, Pencil, Trash2, Calculator, Upload } from 'lucide-react';
import { Button, Spinner, Alert } from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  getReturn,
  listCapitalGains,
  computeTax,
  addCapitalGain,
  updateCapitalGain,
  deleteCapitalGain,
  getCapitalGainInsights,
  filingKeys,
} from '@/features/filing/api';
import type { CapitalGainDto } from '@/features/filing/types';
import type { CapitalGainFormValues } from '@/features/filing/schemas';
import { CapitalGainForm } from '@/features/filing/components/income-forms';
import { CgSummaryDashboard } from '@/features/capital-gains/CgSummaryDashboard';
import { CG_CATEGORIES, categoryOfRow, type CgCategoryKey } from '@/features/capital-gains/categories';
import { GuidedAssistant } from '@/features/capital-gains/GuidedAssistant';
import { CgImportPanel } from '@/features/capital-gains/CgImportPanel';

function toDefaults(row: CapitalGainDto): Partial<CapitalGainFormValues> {
  return {
    assetType: row.assetType,
    term: row.term,
    acquisitionMode: row.acquisitionMode ?? 'Purchase',
    acquisitionDate: row.acquisitionDate ?? '',
    transferDate: row.transferDate ?? '',
    previousOwnerAcquisitionDate: row.previousOwnerAcquisitionDate ?? '',
    previousOwnerCost: row.previousOwnerCost ?? 0,
    isRuralAgriculturalLand: row.isRuralAgriculturalLand ?? false,
    salePrice: row.salePrice,
    costOfAcquisition: row.costOfAcquisition,
    costOfImprovement: row.costOfImprovement,
    expensesOnTransfer: row.expensesOnTransfer,
    exemptionAmount: row.exemptionAmount,
    exemptionSection: row.exemptionSection ?? '',
    reinvestmentAmount: row.reinvestmentAmount,
    fairMarketValue31Jan2018: row.fairMarketValue31Jan2018,
  };
}

export default function CapitalGainsHubPage({ params }: { params: { returnId: string } }) {
  const { returnId } = params;
  const t = useTranslations('cgHub');
  const ti = useTranslations('income');
  const tc = useTranslations('common');
  const qc = useQueryClient();

  const [mode, setMode] = useState<'beginner' | 'pro'>('beginner');
  useEffect(() => {
    const m = typeof window !== 'undefined' ? window.localStorage.getItem('tallyg.cgMode') : null;
    if (m === 'pro' || m === 'beginner') setMode(m);
  }, []);
  const changeMode = (m: 'beginner' | 'pro') => {
    setMode(m);
    try {
      window.localStorage.setItem('tallyg.cgMode', m);
    } catch {
      /* ignore */
    }
  };

  // null = list view; { row } = editing existing; { defaults } = adding new (category pre-set).
  const [editing, setEditing] = useState<{ row?: CapitalGainDto; defaults?: Partial<CapitalGainFormValues> } | null>(null);
  const [busy, setBusy] = useState(false);
  const [catFilter, setCatFilter] = useState<'all' | CgCategoryKey>('all');
  const [search, setSearch] = useState('');
  const [importing, setImporting] = useState(false);

  const detailQ = useQuery({ queryKey: filingKeys.detail(returnId), queryFn: () => getReturn(returnId) });
  const gainsQ = useQuery({ queryKey: filingKeys.gains(returnId), queryFn: () => listCapitalGains(returnId) });
  const computeQ = useQuery({ queryKey: filingKeys.compute(returnId), queryFn: () => computeTax({ returnId }), retry: false });
  const insightsQ = useQuery({ queryKey: ['cg-insights', returnId], queryFn: () => getCapitalGainInsights(returnId) });

  const gains = gainsQ.data ?? [];
  const compute = computeQ.data;
  const regime = detailQ.data?.regime ?? compute?.recommendedRegime ?? 'New';
  const c = compute ? (regime === 'Old' ? compute.old : compute.new) : null;

  // Smart transaction grid: category filter + free-text search (scrip/section/amount).
  const q = search.trim().toLowerCase();
  const filteredGains = gains.filter(
    (g) =>
      (catFilter === 'all' || categoryOfRow(g) === catFilter) &&
      (q === '' ||
        g.assetType.toLowerCase().includes(q) ||
        (g.taxSection ?? '').toLowerCase().includes(q) ||
        String(g.gain).includes(q)),
  );

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: filingKeys.gains(returnId) });
    void qc.invalidateQueries({ queryKey: filingKeys.compute(returnId) });
    void qc.invalidateQueries({ queryKey: ['cg-insights', returnId] });
  };

  async function save(v: CapitalGainFormValues) {
    setBusy(true);
    try {
      const body = {
        ...v,
        acquisitionDate: v.acquisitionDate || null,
        transferDate: v.transferDate || null,
        previousOwnerAcquisitionDate: v.previousOwnerAcquisitionDate || null,
      };
      if (editing?.row) await updateCapitalGain(returnId, editing.row.id, body);
      else await addCapitalGain(returnId, body);
      invalidate();
      setEditing(null);
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: string) {
    setBusy(true);
    try {
      await deleteCapitalGain(returnId, id);
      invalidate();
    } finally {
      setBusy(false);
    }
  }

  if (gainsQ.isLoading || detailQ.isLoading) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner size={28} label={tc('loading')} />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-5 pb-16">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link
            href={`/returns/${returnId}`}
            className="inline-flex items-center gap-1 text-xs font-medium text-ink-500 hover:text-ink-800"
          >
            <ArrowLeft className="h-3.5 w-3.5" aria-hidden="true" /> {t('back')}
          </Link>
          <h1 className="mt-1 text-xl font-bold text-ink-900">{t('title')}</h1>
          <p className="text-sm text-ink-500">{t('subtitle')}</p>
          {insightsQ.data
            ? (() => {
                const ins = insightsQ.data;
                const alerts = ins.insights.filter((i) => i.severity === 'Warning' || i.severity === 'Risk').length;
                const tone =
                  ins.compliance === 'Green'
                    ? 'border-money-200 bg-money-50 text-money-700'
                    : ins.compliance === 'Yellow'
                      ? 'border-payable-200 bg-payable-50 text-payable-800'
                      : 'border-red-200 bg-red-50 text-red-700';
                const dot =
                  ins.compliance === 'Green' ? 'bg-money-500' : ins.compliance === 'Yellow' ? 'bg-payable-500' : 'bg-red-500';
                return (
                  <span className={`mt-2 inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium ${tone}`}>
                    <span className={`h-2 w-2 rounded-full ${dot}`} aria-hidden="true" />
                    {t(`compliance.${ins.compliance}`)} · {ins.score}/100
                    {alerts > 0 ? ` · ${t('alerts', { count: alerts })}` : ''}
                  </span>
                );
              })()
            : null}
        </div>
        {/* Actions */}
        <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            setImporting((v) => !v);
            setEditing(null);
          }}
        >
          <Upload className="h-4 w-4" aria-hidden="true" /> {t('importCta')}
        </Button>
        <div className="inline-flex rounded-xl border border-ink-200 bg-white p-0.5 text-xs font-medium shadow-sm">
          {(['beginner', 'pro'] as const).map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => changeMode(m)}
              className={
                mode === m
                  ? 'rounded-lg bg-brand-600 px-3 py-1.5 text-white'
                  : 'rounded-lg px-3 py-1.5 text-ink-600 hover:bg-ink-100'
              }
            >
              {m === 'beginner' ? t('modeBeginner') : t('modePro')}
            </button>
          ))}
        </div>
        </div>
      </div>

      {importing ? (
        <CgImportPanel returnId={returnId} onClose={() => setImporting(false)} onImported={invalidate} />
      ) : null}

      {editing ? (
        <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
          <h2 className="mb-3 text-sm font-semibold text-ink-900">
            {editing.row ? t('editTxn') : t('addTxn')}
          </h2>
          <CapitalGainForm
            defaultValues={editing.row ? toDefaults(editing.row) : editing.defaults}
            onSubmit={save}
            onCancel={() => setEditing(null)}
            loading={busy}
          />
        </div>
      ) : (
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_320px]">
         <div className="space-y-5">
          {/* Summary dashboard */}
          {c ? (
            <CgSummaryDashboard result={c} gains={gains} />
          ) : (
            <Alert variant="info">{t('needCompute')}</Alert>
          )}

          {/* Asset-category grid */}
          <div>
            <h2 className="mb-2 text-sm font-semibold text-ink-900">{t('categoriesTitle')}</h2>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
              {CG_CATEGORIES.map((cat) => {
                const Icon = cat.icon;
                const rows = gains.filter((g) => categoryOfRow(g) === (cat.key as CgCategoryKey));
                const net = rows.reduce((s, g) => s + g.gain, 0);
                return (
                  <button
                    key={cat.key}
                    type="button"
                    onClick={() => setEditing({ defaults: { assetType: cat.defaultAssetType } })}
                    className="group flex flex-col rounded-2xl border border-ink-200 bg-white p-4 text-left shadow-card transition-colors hover:border-brand-300 hover:bg-brand-50/40"
                  >
                    <span className="inline-flex h-9 w-9 items-center justify-center rounded-xl bg-brand-50 text-brand-600">
                      <Icon className="h-5 w-5" aria-hidden="true" />
                    </span>
                    <span className="mt-3 text-sm font-semibold text-ink-900">{t(`cat.${cat.key}`)}</span>
                    <span className="mt-0.5 text-xs text-ink-500">
                      {rows.length === 0 ? t('catEmpty') : t('catCount', { count: rows.length })}
                    </span>
                    {rows.length > 0 ? (
                      <span className={`mt-1 text-sm font-medium ${net < 0 ? 'text-payable-700' : 'text-money-700'}`}>
                        {formatInr(net)}
                      </span>
                    ) : (
                      <span className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-brand-600 opacity-0 transition-opacity group-hover:opacity-100">
                        <Plus className="h-3.5 w-3.5" aria-hidden="true" /> {t('add')}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Pro mode: smart transaction grid (filter by category + search) with inline edit/delete */}
          {mode === 'pro' && gains.length > 0 ? (
            <div className="rounded-2xl border border-ink-200 bg-white shadow-card">
              <div className="flex flex-wrap items-center gap-2 border-b border-ink-100 px-4 py-3">
                <h2 className="mr-auto text-sm font-semibold text-ink-900">{t('txnsTitle')}</h2>
                <select
                  value={catFilter}
                  onChange={(e) => setCatFilter(e.target.value as 'all' | CgCategoryKey)}
                  className="rounded-lg border border-ink-200 bg-white px-2 py-1 text-xs text-ink-700"
                >
                  <option value="all">{t('filterAll')}</option>
                  {CG_CATEGORIES.map((cat) => (
                    <option key={cat.key} value={cat.key}>
                      {t(`cat.${cat.key}`)}
                    </option>
                  ))}
                </select>
                <input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder={t('searchPlaceholder')}
                  className="w-40 rounded-lg border border-ink-200 px-2 py-1 text-xs text-ink-800 placeholder:text-ink-400"
                />
                <Button size="sm" variant="outline" onClick={() => setEditing({ defaults: {} })}>
                  <Plus className="h-4 w-4" aria-hidden="true" /> {t('add')}
                </Button>
              </div>
              <ul className="divide-y divide-ink-100">
                {filteredGains.map((g) => (
                  <li key={g.id} className="flex items-center justify-between gap-3 px-4 py-3">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-ink-800">
                        {ti(`asset.${g.assetType}`)} · {g.term === 'Long' ? ti('longTerm') : ti('shortTerm')}
                        {g.coOwnerPercent > 0 && g.coOwnerPercent < 100 ? ` · ${g.coOwnerPercent}%` : ''}
                      </p>
                      <p className="text-xs text-ink-500">
                        {ti('gain')}: <span className="font-medium text-ink-700">{formatInr(g.gain)}</span>
                        {g.taxSection ? ` · ${g.taxSection}` : ''}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-1">
                      <button
                        type="button"
                        aria-label={tc('edit')}
                        onClick={() => setEditing({ row: g })}
                        className="rounded-lg p-2 text-ink-500 hover:bg-ink-100"
                      >
                        <Pencil className="h-4 w-4" aria-hidden="true" />
                      </button>
                      <button
                        type="button"
                        aria-label={tc('delete')}
                        disabled={busy}
                        onClick={() => void remove(g.id)}
                        className="rounded-lg p-2 text-red-600 hover:bg-red-50 disabled:opacity-50"
                      >
                        <Trash2 className="h-4 w-4" aria-hidden="true" />
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
              {filteredGains.length === 0 ? (
                <p className="px-4 py-6 text-center text-sm text-ink-500">{t('noMatches')}</p>
              ) : null}
            </div>
          ) : null}

          {/* Link to the full computation */}
          <div className="flex justify-end">
            <Link
              href={`/returns/${returnId}/workspace`}
              className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-600 hover:text-brand-700"
            >
              <Calculator className="h-4 w-4" aria-hidden="true" /> {t('viewComputation')}
            </Link>
          </div>
         </div>

          {/* Guided tax assistant rail */}
          <aside className="h-max xl:sticky xl:top-4">
            <GuidedAssistant gains={gains} result={c} />
          </aside>
        </div>
      )}
    </div>
  );
}
