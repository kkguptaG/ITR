'use client';

// ---------------------------------------------------------------------------
// features/shared/DeductionSuggestionCard — 80C/80D tax-saving nudges.
//
// Renders the recommender's output (docs 03 §80C/80D): for each section with
// unused headroom, shows how much more the user could invest and the estimated
// tax it would save, with an optional "Add" CTA. Self-contained — takes a list
// of DeductionSuggestionView; the wizard's deductions step maps its API result
// onto these props.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { PiggyBank, TrendingUp, Plus, CheckCircle2 } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, Button } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, toNumber } from '@/lib/format';
import type { DeductionSuggestionView } from './types';

export interface DeductionSuggestionCardProps {
  suggestions: DeductionSuggestionView[];
  /** Optional: invoked with the section code when the user taps "Add". */
  onAdd?: (section: string) => void;
  /** Disables the per-row CTA (e.g. while saving). */
  busy?: boolean;
  className?: string;
}

export function DeductionSuggestionCard({
  suggestions,
  onAdd,
  busy,
  className,
}: DeductionSuggestionCardProps) {
  const t = useTranslations('shared');

  // Only sections that still have room are worth nudging.
  const actionable = suggestions.filter((s) => toNumber(s.headroom) > 0);

  const totalPotential = actionable.reduce(
    (sum, s) => sum + toNumber(s.potentialSaving),
    0,
  );

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <PiggyBank className="h-5 w-5 text-brand-600" aria-hidden="true" />
          {t('deductionSuggestTitle')}
        </CardTitle>
        <CardDescription>{t('deductionSuggestSubtitle')}</CardDescription>
      </CardHeader>
      <CardContent>
        {actionable.length === 0 ? (
          <div className="flex items-start gap-3 rounded-xl bg-money-50 p-4">
            <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-money-600" aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-money-800">{t('deductionMaxedTitle')}</p>
              <p className="mt-0.5 text-sm text-money-700">{t('deductionMaxedBody')}</p>
            </div>
          </div>
        ) : (
          <ul className="space-y-2.5">
            {actionable.map((s) => {
              const headroom = toNumber(s.headroom);
              const saving = toNumber(s.potentialSaving);
              return (
                <li
                  key={s.section}
                  className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-ink-200 p-3.5"
                >
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="rounded-md bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                        {s.section}
                      </span>
                      {s.title && (
                        <span className="truncate text-sm font-medium text-ink-800">
                          {s.title}
                        </span>
                      )}
                    </div>
                    <p className="mt-1 text-sm text-ink-600">
                      {t('deductionHeadroom', { amount: formatInr(headroom) })}
                    </p>
                    {saving > 0 && (
                      <p className="mt-0.5 inline-flex items-center gap-1 text-xs font-medium text-money-700">
                        <TrendingUp className="h-3.5 w-3.5" aria-hidden="true" />
                        {t('deductionSaving', { amount: formatInr(saving) })}
                      </p>
                    )}
                    {s.note && <p className="mt-0.5 text-xs text-ink-400">{s.note}</p>}
                  </div>

                  {onAdd && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      loading={busy}
                      onClick={() => onAdd(s.section)}
                    >
                      <Plus className="h-4 w-4" aria-hidden="true" />
                      {t('deductionAdd')}
                    </Button>
                  )}
                </li>
              );
            })}
          </ul>
        )}

        {actionable.length > 0 && totalPotential > 0 && (
          <p
            className={cn(
              'mt-3 rounded-lg bg-money-50 px-3 py-2 text-center text-sm font-medium text-money-700',
            )}
          >
            {t('deductionTotalPotential', { amount: formatInr(totalPotential) })}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
