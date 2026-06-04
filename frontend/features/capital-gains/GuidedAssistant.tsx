'use client';

import { useTranslations } from 'next-intl';
import { Lightbulb, TriangleAlert, Info, CheckCircle2, BookOpen } from 'lucide-react';
import { formatInr } from '@/lib/format';
import type { CapitalGainDto, TaxComputationResultDto } from '@/features/filing/types';

type Tone = 'warning' | 'info' | 'success' | 'tip';

const TONE_STYLE: Record<Tone, { wrap: string; icon: typeof Info }> = {
  warning: { wrap: 'border-payable-200 bg-payable-50 text-payable-800', icon: TriangleAlert },
  info: { wrap: 'border-sky-200 bg-sky-50 text-sky-900', icon: Info },
  success: { wrap: 'border-money-200 bg-money-50 text-money-900', icon: CheckCircle2 },
  tip: { wrap: 'border-brand-200 bg-brand-50 text-brand-800', icon: Lightbulb },
};

/**
 * The right-rail Guided Tax Assistant (Layer 4C): plain-language, contextual insights derived from the
 * captured rows + the live computation — warnings (missing data, VDA rules), opportunities (exemptions,
 * loss set-off) and reminders (advance tax) — plus a quick glossary. Read-only; the deeper rule-based
 * risk/optimization engine arrives in P6.
 */
export function GuidedAssistant({ gains, result }: { gains: CapitalGainDto[]; result: TaxComputationResultDto | null }) {
  const t = useTranslations('cgHub');

  const insights: { tone: Tone; text: string }[] = [];

  const missingDates = gains.filter((g) => !g.acquisitionDate || !g.transferDate).length;
  if (missingDates > 0) insights.push({ tone: 'warning', text: t('warnDates', { count: missingDates }) });

  if (gains.some((g) => g.assetType === 'CryptoVda')) insights.push({ tone: 'info', text: t('tipVda') });

  if (result && result.specialIncome.ltcg112A > 0) insights.push({ tone: 'success', text: t('tip112A') });

  const lossesCf =
    Math.max(0, result?.shortTermCapitalLossCarriedForward ?? 0) + Math.max(0, result?.longTermCapitalLossCarriedForward ?? 0);
  if (lossesCf > 0) insights.push({ tone: 'info', text: t('tipLossesCf', { amount: formatInr(lossesCf) }) });

  const propLtcgNoExemption = gains.some(
    (g) =>
      g.term === 'Long' &&
      (g.assetType === 'ImmovableProperty' || g.assetType === 'AgriculturalLand') &&
      g.gain > 0 &&
      !g.exemptionSection,
  );
  if (propLtcgNoExemption) insights.push({ tone: 'tip', text: t('tip54') });

  if (result && result.taxAtSpecialRates > 10000) insights.push({ tone: 'info', text: t('tipAdvanceTax') });

  if (insights.length === 0) insights.push({ tone: 'success', text: t('allGood') });

  const glossary = ['glossStcg', 'glossLtcg', 'glossIndex', 'gloss112A'] as const;

  return (
    <div className="space-y-3">
      <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
        <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900">
          <Lightbulb className="h-4 w-4 text-brand-500" aria-hidden="true" /> {t('assistantTitle')}
        </h3>
        <ul className="space-y-2">
          {insights.map((ins, i) => {
            const { wrap, icon: Icon } = TONE_STYLE[ins.tone];
            return (
              <li key={i} className={`flex gap-2 rounded-xl border p-2.5 text-xs leading-relaxed ${wrap}`}>
                <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                <span>{ins.text}</span>
              </li>
            );
          })}
        </ul>
      </div>

      <details className="group rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
        <summary className="flex cursor-pointer list-none items-center gap-2 text-sm font-semibold text-ink-900">
          <BookOpen className="h-4 w-4 text-ink-400" aria-hidden="true" /> {t('glossaryTitle')}
        </summary>
        <dl className="mt-3 space-y-2 text-xs text-ink-600">
          {glossary.map((g) => (
            <div key={g}>
              <dt className="font-medium text-ink-800">{t(`${g}.t`)}</dt>
              <dd>{t(`${g}.d`)}</dd>
            </div>
          ))}
        </dl>
      </details>
    </div>
  );
}
