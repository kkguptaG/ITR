'use client';

// ---------------------------------------------------------------------------
// DeductionRecommendations — renders POST /tax/recommendations: the 80C/80D
// gap-analysis advisor. Shows the headline, whether switching regime beats
// deductions, and a ranked list of "invest ₹X under §80C to save ₹Y" tips.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Sparkles, TrendingUp } from 'lucide-react';
import { Alert, Badge, Card } from '@/components/ui';
import { formatInr } from '@/lib/format';
import type { RecommendationsResponse } from '../types';

export function DeductionRecommendations({ data }: { data: RecommendationsResponse }) {
  const t = useTranslations('wizard');

  return (
    <Card className="p-5">
      <div className="mb-3 flex items-center gap-2">
        <Sparkles className="h-5 w-5 text-brand-600" aria-hidden="true" />
        <h3 className="font-semibold text-ink-900">{t('recommendationsTitle')}</h3>
      </div>

      {data.headline && <p className="mb-3 text-sm text-ink-600">{data.headline}</p>}

      {data.regimeSwitchBeatsDeductions && (
        <Alert variant="info" className="mb-3">
          {t('regimeBeatsDeductions')}
        </Alert>
      )}

      {data.suggestions.length === 0 ? (
        <p className="text-sm text-ink-500">{t('noSuggestions')}</p>
      ) : (
        <ul className="space-y-2">
          {data.suggestions.map((s) => (
            <li
              key={`${s.section}-${s.rank}`}
              className="flex items-start justify-between gap-3 rounded-xl border border-ink-200 p-3"
            >
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <Badge tone="brand">{s.section}</Badge>
                  <span className="text-sm font-medium text-ink-800">{s.label}</span>
                </div>
                {s.utilityNote && <p className="mt-0.5 text-xs text-ink-500">{s.utilityNote}</p>}
                <p className="mt-1 text-xs text-ink-500">
                  {t('investToSave', {
                    invest: formatInr(s.gapToInvest),
                    save: formatInr(s.marginalTaxSaved),
                  })}
                </p>
              </div>
              <div className="shrink-0 text-right">
                <div className="inline-flex items-center gap-1 text-money-700">
                  <TrendingUp className="h-3.5 w-3.5" aria-hidden="true" />
                  <span className="text-sm font-semibold tabular-nums">{formatInr(s.marginalTaxSaved)}</span>
                </div>
                <div className="text-[11px] text-ink-400">{t('taxSaved')}</div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
