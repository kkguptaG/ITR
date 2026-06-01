'use client';

// ---------------------------------------------------------------------------
// RegimeCompareCard — old vs new tax regime, side by side, with the recommended
// option highlighted, the savings delta, and a "choose this regime" action per
// column. Renders directly from the engine's ComputeResponse (never re-derives).
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Check, Sparkles, TrendingDown } from 'lucide-react';
import { Button, Card } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import type { Regime } from '@/lib/api-types';
import type { ComputeResponse, TaxComputationResultDto } from '../types';

function RegimeColumn({
  label,
  comp,
  recommended,
  selected,
  onChoose,
  chooseLabel,
}: {
  label: string;
  comp: TaxComputationResultDto;
  recommended: boolean;
  selected: boolean;
  onChoose?: () => void;
  chooseLabel: string;
}) {
  const t = useTranslations('wizard');
  const refund = comp.refundOrPayable;
  return (
    <Card
      className={cn(
        'relative flex flex-col p-5 transition-shadow',
        recommended ? 'ring-2 ring-brand-500' : 'ring-1 ring-ink-200',
      )}
    >
      {recommended && (
        <span className="absolute -top-3 left-5 inline-flex items-center gap-1 rounded-full bg-brand-600 px-2.5 py-0.5 text-xs font-semibold text-white">
          <Sparkles className="h-3 w-3" aria-hidden="true" />
          {t('recommended')}
        </span>
      )}
      <div className="mb-3 flex items-baseline justify-between">
        <h3 className="text-base font-semibold text-ink-900">{label}</h3>
        {selected && (
          <span className="inline-flex items-center gap-1 text-xs font-medium text-brand-700">
            <Check className="h-3.5 w-3.5" aria-hidden="true" /> {t('selected')}
          </span>
        )}
      </div>

      <dl className="space-y-1.5 text-sm">
        <Row label={t('taxableIncome')} value={formatInr(comp.taxableIncome)} />
        <Row label={t('totalTax')} value={formatInr(comp.totalTax)} strong />
        <Row label={t('tdsPaid')} value={formatInr(comp.tdsPaid)} muted />
      </dl>

      <div className="mt-3 border-t border-ink-100 pt-3">
        <div className="text-xs text-ink-500">{refund >= 0 ? t('refund') : t('payable')}</div>
        <div
          className={cn(
            'text-xl font-bold tabular-nums',
            refund >= 0 ? 'text-money-600' : 'text-payable-700',
          )}
        >
          {formatInr(Math.abs(refund))}
        </div>
      </div>

      {onChoose && (
        <Button
          variant={selected ? 'secondary' : recommended ? 'primary' : 'outline'}
          className="mt-4"
          onClick={onChoose}
          fullWidth
        >
          {selected ? t('selected') : chooseLabel}
        </Button>
      )}
    </Card>
  );
}

function Row({ label, value, strong, muted }: { label: string; value: string; strong?: boolean; muted?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className={cn('text-ink-500', muted && 'text-ink-400')}>{label}</dt>
      <dd className={cn('tabular-nums text-ink-800', strong && 'font-semibold text-ink-900')}>{value}</dd>
    </div>
  );
}

export function RegimeCompareCard({
  data,
  selected,
  onChoose,
}: {
  data: ComputeResponse;
  selected: Regime | null;
  onChoose?: (regime: Regime) => void;
}) {
  const t = useTranslations('wizard');
  const recommended = data.recommendedRegime;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <RegimeColumn
          label={t('oldRegime')}
          comp={data.old}
          recommended={recommended === 'Old'}
          selected={selected === 'Old'}
          onChoose={onChoose ? () => onChoose('Old') : undefined}
          chooseLabel={t('chooseOld')}
        />
        <RegimeColumn
          label={t('newRegime')}
          comp={data.new}
          recommended={recommended === 'New'}
          selected={selected === 'New'}
          onChoose={onChoose ? () => onChoose('New') : undefined}
          chooseLabel={t('chooseNew')}
        />
      </div>

      {data.savingsVsAlternative > 0 && (
        <div className="flex items-start gap-2 rounded-xl bg-money-50 p-3.5 text-sm text-money-800">
          <TrendingDown className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <span>
            {t('youSaveRegime', {
              regime: recommended === 'Old' ? t('oldRegime') : t('newRegime'),
              amount: formatInr(data.savingsVsAlternative),
            })}
            {data.reason ? ` — ${data.reason}` : ''}
          </span>
        </div>
      )}
    </div>
  );
}
