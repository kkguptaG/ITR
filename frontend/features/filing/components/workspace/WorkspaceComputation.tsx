'use client';

// WorkspaceComputation — the centre of the Computation Workspace: a search/expand
// toolbar, one expandable card per income head (Particulars / Amount / Notes /
// Status), and a totals strip. Head NET amounts are authoritative (from the tax
// engine); the expanded rows list the underlying entries for context.

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { CheckCircle2, ChevronDown, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';

export interface WorkspaceEntry {
  label: string;
  amount: number;
  note?: string | null;
}
export interface WorkspaceSection {
  id: string;
  letter: string;
  title: string;
  icon: LucideIcon;
  net: number;
  entries: WorkspaceEntry[];
}

export function WorkspaceComputation({
  sections,
  gti,
  totalDeductions,
  taxableIncome,
  taxPayable,
  regime,
}: {
  sections: WorkspaceSection[];
  gti: number;
  totalDeductions: number;
  taxableIncome: number;
  taxPayable: number;
  regime: string;
}) {
  const t = useTranslations('workspace');
  const [open, setOpen] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(sections.map((s) => [s.id, true])),
  );
  const allOpen = sections.length > 0 && sections.every((s) => open[s.id]);
  const toggleAll = () => {
    const v = !allOpen;
    setOpen(Object.fromEntries(sections.map((s) => [s.id, v])));
  };

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <input
          disabled
          placeholder={t('search')}
          className="h-10 min-w-[220px] flex-1 rounded-xl border border-ink-200 bg-white px-3.5 text-sm text-ink-500 placeholder:text-ink-400"
        />
        <div className="flex items-center gap-2 text-sm">
          <button
            type="button"
            onClick={toggleAll}
            className="rounded-lg border border-ink-200 px-3 py-1.5 font-medium text-ink-700 transition-colors hover:bg-ink-50"
          >
            {allOpen ? t('collapseAll') : t('expandAll')}
          </button>
          <span className="inline-flex items-center gap-1.5 rounded-lg bg-money-50 px-3 py-1.5 font-medium text-money-700">
            <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            {t('noErrors')}
          </span>
        </div>
      </div>

      {/* Column header */}
      <div className="hidden grid-cols-[1.5fr_1fr_1.2fr_auto] gap-4 px-4 text-xs font-medium uppercase tracking-wide text-ink-400 sm:grid">
        <span>{t('particulars')}</span>
        <span className="text-right">{t('amount')}</span>
        <span>{t('notes')}</span>
        <span>{t('status')}</span>
      </div>

      {/* Head sections */}
      {sections.map((s) => {
        const isOpen = open[s.id];
        const Icon = s.icon;
        return (
          <Card key={s.id} id={`ws-${s.id}`} className="scroll-mt-24 overflow-hidden">
            <button
              type="button"
              onClick={() => setOpen((o) => ({ ...o, [s.id]: !o[s.id] }))}
              className="flex w-full items-center gap-3 px-4 py-3.5 text-left transition-colors hover:bg-ink-50/60"
            >
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
                <Icon className="h-5 w-5" aria-hidden="true" />
              </span>
              <span className="flex-1 truncate font-semibold text-ink-900">
                {s.letter}. {s.title}
              </span>
              <span className="hidden rounded-full bg-money-50 px-2 py-0.5 text-xs font-medium text-money-700 sm:inline">
                {t('calculated')}
              </span>
              <span className="w-28 text-right font-semibold tabular-nums text-ink-900 sm:w-32">{formatInr(s.net)}</span>
              <CheckCircle2 className="h-5 w-5 shrink-0 text-money-500" aria-hidden="true" />
              <ChevronDown className={cn('h-4 w-4 shrink-0 text-ink-400 transition-transform', isOpen && 'rotate-180')} aria-hidden="true" />
            </button>

            {isOpen && (
              <div className="border-t border-ink-100 px-4 py-2">
                {s.entries.length === 0 ? (
                  <p className="py-2 text-sm text-ink-400">{t('noEntries', { amount: formatInr(s.net) })}</p>
                ) : (
                  s.entries.map((e, i) => (
                    <div key={i} className="grid grid-cols-[1fr_auto] items-center gap-4 py-1.5 text-sm sm:grid-cols-[1.5fr_1fr_1.2fr_auto]">
                      <span className="truncate text-ink-600">{e.label}</span>
                      <span className="text-right tabular-nums text-ink-800">{formatInr(e.amount)}</span>
                      <span className="hidden truncate text-xs text-ink-400 sm:block">{e.note ?? '—'}</span>
                      <span className="hidden sm:block" />
                    </div>
                  ))
                )}
                <div className="mt-1 flex items-center justify-between border-t border-ink-100 pt-2 text-sm font-semibold">
                  <span className="text-ink-700">{t('netTotal')}</span>
                  <span className="tabular-nums text-ink-900">{formatInr(s.net)}</span>
                </div>
              </div>
            )}
          </Card>
        );
      })}

      {/* Totals strip */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {[
          { label: t('totalGti'), value: gti, tone: 'text-ink-900' },
          { label: t('totalDeductions'), value: totalDeductions, tone: 'text-money-700' },
          { label: t('totalTaxable'), value: taxableIncome, tone: 'text-ink-900' },
          { label: t('totalTaxPayable', { regime }), value: taxPayable, tone: 'text-payable-700' },
        ].map((k) => (
          <div key={k.label} className="rounded-xl border border-ink-200 bg-white p-4">
            <p className="text-[11px] font-medium uppercase tracking-wide text-ink-400">{k.label}</p>
            <p className={cn('mt-1 text-xl font-semibold tabular-nums', k.tone)}>{formatInr(k.value)}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
