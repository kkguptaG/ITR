'use client';

// ---------------------------------------------------------------------------
// ItrRecommendation — renders the auto-selector verdict (GET /returns/selector)
// as a friendly recommendation chip with the deciding flags + a one-click
// "use the recommended form" action. Used on the Personal step.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Lightbulb } from 'lucide-react';
import { Button } from '@/components/ui';
import { formatItrType } from '@/features/returns/helpers';
import type { ItrSelectionVerdict } from '../types';

export function ItrRecommendation({
  verdict,
  current,
  onApply,
}: {
  verdict: ItrSelectionVerdict;
  current: string | null;
  onApply: (itr: string) => void;
}) {
  const t = useTranslations('wizard');
  const matches = current === verdict.recommendedForm;

  return (
    <div className="flex flex-col gap-3 rounded-xl border border-brand-200 bg-brand-50 p-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-start gap-2.5">
        <Lightbulb className="mt-0.5 h-5 w-5 shrink-0 text-brand-600" aria-hidden="true" />
        <div className="space-y-0.5 text-sm">
          <p className="font-medium text-brand-900">
            {t('recommendForm', { form: formatItrType(verdict.recommendedForm) })}
          </p>
          {verdict.explanation && <p className="text-brand-800/80">{verdict.explanation}</p>}
        </div>
      </div>
      {!matches && (
        <Button
          variant="outline"
          size="sm"
          className="shrink-0 bg-white"
          onClick={() => onApply(verdict.recommendedForm)}
        >
          {t('useRecommended')}
        </Button>
      )}
    </div>
  );
}
