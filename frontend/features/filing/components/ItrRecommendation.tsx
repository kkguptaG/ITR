'use client';

// ---------------------------------------------------------------------------
// ItrRecommendation — renders the auto-selector verdict (GET /returns/selector)
// as a friendly recommendation chip with the deciding flags + a one-click
// "use the recommended form" action. Used on the Personal step.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Lightbulb, XCircle } from 'lucide-react';
import { Button } from '@/components/ui';
import { formatItrType } from '@/features/returns/helpers';
import type { ItrSelectionVerdict } from '../types';

const FLAG_LABELS: Record<string, string> = {
  has_capital_gains: 'capital gains',
  has_crypto_vda: 'crypto / VDA',
  has_winnings: 'lottery / gaming winnings',
  has_foreign_assets: 'foreign assets or income',
  has_business_income: 'business income (regular books)',
  has_speculative_income: 'speculative (intraday) income',
  has_fno_income: 'F&O / derivatives income',
  has_presumptive_income: 'presumptive business income',
  regular_books_business: 'regular-books business',
  income_gt_50L: 'income above ₹50 lakh',
  multiple_house_properties: 'more than one house property',
  brought_forward_loss: 'brought-forward loss',
  non_resident_or_rnor: 'NR / RNOR status',
  director_or_unlisted_shares: 'directorship or unlisted shares',
  partner_in_firm: 'partner in a firm',
  agri_income_gt_5000: 'agricultural income above ₹5,000',
};

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

  // Collect blockers from forms simpler than the recommended one so we can explain
  // why the user can't use a simpler form (e.g. why ITR-1 is blocked).
  const blockedSimpler = Object.entries(verdict.blockedForms ?? {})
    .filter(([form]) => {
      const order: Record<string, number> = { 'ITR-1': 1, 'ITR-4': 1, 'ITR-2': 2, 'ITR-3': 3 };
      return (order[form] ?? 99) < (order[verdict.recommendedForm] ?? 99);
    })
    .flatMap(([, flags]) => flags)
    .map((f) => FLAG_LABELS[f] ?? f.replace(/_/g, ' '))
    .filter((v, i, a) => a.indexOf(v) === i)   // deduplicate
    .slice(0, 3);

  return (
    <div className="space-y-2 rounded-xl border border-brand-200 bg-brand-50 p-4">
      <div className="flex items-start justify-between gap-3">
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

      {blockedSimpler.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5 pt-0.5">
          <XCircle className="h-3.5 w-3.5 shrink-0 text-payable-600" aria-hidden="true" />
          <span className="text-xs text-ink-600">
            Simpler form blocked by:{' '}
            {blockedSimpler.map((label, i) => (
              <span key={label}>
                <span className="font-medium text-ink-800">{label}</span>
                {i < blockedSimpler.length - 1 ? ', ' : ''}
              </span>
            ))}
          </span>
        </div>
      )}
    </div>
  );
}
