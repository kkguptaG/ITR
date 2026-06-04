'use client';

import { useTranslations } from 'next-intl';
import { TrendingUp, TrendingDown, Layers, Receipt } from 'lucide-react';
import { formatInr } from '@/lib/format';
import type { CapitalGainDto, TaxComputationResultDto } from '@/features/filing/types';

// One rate bucket of Schedule SI (the "income chargeable at special rates" view).
function rateRows(si: TaxComputationResultDto['specialIncome'], t: (k: string) => string) {
  return [
    { key: 'stcg111A', label: t('rate111A'), amount: si.stcg111A, tone: 'bg-brand-500' },
    { key: 'ltcg112A', label: t('rate112A'), amount: si.ltcg112A, tone: 'bg-money-500' },
    { key: 'ltcg112', label: t('rate112'), amount: si.ltcg112, tone: 'bg-emerald-600' },
    { key: 'vda', label: t('rateVda'), amount: si.vda115BBH, tone: 'bg-amber-500' },
    { key: 'slab', label: t('rateSlab'), amount: si.slabRateCapitalGains, tone: 'bg-ink-400' },
  ].filter((r) => r.amount > 0);
}

export function CgSummaryDashboard({ result, gains }: { result: TaxComputationResultDto; gains: CapitalGainDto[] }) {
  const t = useTranslations('cgHub');

  const stcg = gains.filter((g) => g.term === 'Short').reduce((s, g) => s + Math.max(0, g.gain), 0);
  const ltcg = gains.filter((g) => g.term === 'Long').reduce((s, g) => s + Math.max(0, g.gain), 0);
  const lossesCf =
    Math.max(0, result.shortTermCapitalLossCarriedForward) + Math.max(0, result.longTermCapitalLossCarriedForward);

  const kpis = [
    { label: t('kpiStcg'), value: stcg, icon: TrendingUp, tone: 'text-brand-700 bg-brand-50' },
    { label: t('kpiLtcg'), value: ltcg, icon: TrendingUp, tone: 'text-money-700 bg-money-50' },
    { label: t('kpiNet'), value: result.capitalGainsNetIncome, icon: Layers, tone: 'text-ink-700 bg-ink-100' },
    { label: t('kpiLossesCf'), value: lossesCf, icon: TrendingDown, tone: 'text-payable-700 bg-payable-50' },
  ];

  const rows = rateRows(result.specialIncome, t);
  const rateTotal = rows.reduce((s, r) => s + r.amount, 0);

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {kpis.map((k) => {
          const Icon = k.icon;
          return (
            <div key={k.label} className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <span className={`inline-flex h-9 w-9 items-center justify-center rounded-xl ${k.tone}`}>
                <Icon className="h-5 w-5" aria-hidden="true" />
              </span>
              <p className="mt-3 text-xs font-medium uppercase tracking-wide text-ink-400">{k.label}</p>
              <p className="mt-0.5 text-xl font-semibold text-ink-900">{formatInr(k.value)}</p>
            </div>
          );
        })}
      </div>

      <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
        <div className="mb-3 flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-ink-900">
            <Receipt className="h-4 w-4 text-ink-400" aria-hidden="true" /> {t('rateTitle')}
          </h3>
          <span className="text-xs text-ink-500">
            {t('taxOnGains')}: <span className="font-semibold text-ink-800">{formatInr(result.taxAtSpecialRates)}</span>
          </span>
        </div>
        {rows.length === 0 ? (
          <p className="text-sm text-ink-500">{t('noRateIncome')}</p>
        ) : (
          <ul className="space-y-2.5">
            {rows.map((r) => (
              <li key={r.key}>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-ink-700">{r.label}</span>
                  <span className="font-medium text-ink-900">{formatInr(r.amount)}</span>
                </div>
                <div className="mt-1 h-2 overflow-hidden rounded-full bg-ink-100">
                  <div
                    className={`h-full rounded-full ${r.tone}`}
                    style={{ width: `${rateTotal > 0 ? Math.max(3, (r.amount / rateTotal) * 100) : 0}%` }}
                  />
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
