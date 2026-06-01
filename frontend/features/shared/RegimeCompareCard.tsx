'use client';

// ---------------------------------------------------------------------------
// features/shared/RegimeCompareCard — Old vs New regime, side by side.
//
// Shows total tax under each regime, badges the cheaper one as "Recommended",
// states the saving, and (optionally) lets the user choose a regime via
// onChoose. Display-only if no callback is passed. Self-contained — accepts two
// ComputationView objects + the recommended regime; the wizard maps its
// RegimeComparisonDto onto these props.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Sparkles, Check } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent, Button, Badge } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, toNumber } from '@/lib/format';
import type { Regime } from '@/lib/api-types';
import type { ComputationView } from './types';

export interface RegimeCompareCardProps {
  oldRegime: ComputationView;
  newRegime: ComputationView;
  recommended: Regime;
  /** Savings of the recommended regime over the other (absolute amount). */
  savings?: number | string;
  /** Currently selected regime (highlights the chosen column). */
  selected?: Regime | null;
  /** When provided, renders a choose button per column. */
  onChoose?: (regime: Regime) => void;
  /** Disables the choose buttons (e.g. while a mutation is in flight). */
  busy?: boolean;
  className?: string;
}

interface ColProps {
  regime: Regime;
  computation: ComputationView;
  isRecommended: boolean;
  isSelected: boolean;
  onChoose?: (regime: Regime) => void;
  busy?: boolean;
}

function RegimeColumn({ regime, computation, isRecommended, isSelected, onChoose, busy }: ColProps) {
  const t = useTranslations('shared');
  const totalTax = toNumber(computation.totalTax);
  const outcome = toNumber(computation.refundOrPayable);
  const isRefund = outcome >= 0;
  const name = regime === 'Old' ? t('regimeOld') : t('regimeNew');

  return (
    <div
      className={cn(
        'relative flex flex-col rounded-xl border p-4 transition-colors',
        isSelected
          ? 'border-brand-400 bg-brand-50/60 ring-1 ring-brand-300'
          : isRecommended
            ? 'border-money-300 bg-money-50/50'
            : 'border-ink-200 bg-white',
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-semibold text-ink-900">{name}</span>
        {isRecommended && (
          <Badge tone="success" className="gap-1">
            <Sparkles className="h-3 w-3" aria-hidden="true" />
            {t('recommended')}
          </Badge>
        )}
      </div>

      <div className="mt-3">
        <p className="text-xs text-ink-500">{t('totalTax')}</p>
        <p className="mt-0.5 text-2xl font-semibold tabular-nums text-ink-900">
          {formatInr(totalTax)}
        </p>
      </div>

      <div className="mt-2 text-xs">
        <span className={cn(isRefund ? 'text-money-700' : 'text-payable-700')}>
          {isRefund ? t('refundDue') : t('taxPayable')}: {formatInr(Math.abs(outcome))}
        </span>
      </div>

      {onChoose && (
        <Button
          type="button"
          variant={isSelected ? 'primary' : 'outline'}
          size="sm"
          fullWidth
          loading={busy}
          onClick={() => onChoose(regime)}
          className="mt-4"
          aria-pressed={isSelected}
        >
          {isSelected ? (
            <>
              <Check className="h-4 w-4" aria-hidden="true" />
              {t('selected')}
            </>
          ) : (
            t('chooseRegime', { regime: name })
          )}
        </Button>
      )}
    </div>
  );
}

export function RegimeCompareCard({
  oldRegime,
  newRegime,
  recommended,
  savings,
  selected,
  onChoose,
  busy,
  className,
}: RegimeCompareCardProps) {
  const t = useTranslations('shared');
  const savingAmount = savings !== undefined ? toNumber(savings) : null;

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>{t('regimeCompareTitle')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <RegimeColumn
            regime="Old"
            computation={oldRegime}
            isRecommended={recommended === 'Old'}
            isSelected={selected === 'Old'}
            onChoose={onChoose}
            busy={busy}
          />
          <RegimeColumn
            regime="New"
            computation={newRegime}
            isRecommended={recommended === 'New'}
            isSelected={selected === 'New'}
            onChoose={onChoose}
            busy={busy}
          />
        </div>

        {savingAmount !== null && savingAmount > 0 && (
          <div className="flex items-center justify-center gap-2 rounded-xl bg-money-50 px-4 py-2.5 text-sm font-medium text-money-700">
            <Sparkles className="h-4 w-4" aria-hidden="true" />
            {t('youSaveWithRegime', {
              amount: formatInr(savingAmount),
              regime: recommended === 'Old' ? t('regimeOld') : t('regimeNew'),
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
