'use client';

// ---------------------------------------------------------------------------
// PlanPicker — radio-style selection of a filing-fee plan (GET /pricing/plans).
// Renders price + feature bullets; the selected plan is highlighted. Controlled.
// ---------------------------------------------------------------------------

import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import { Spinner } from '@/components/ui';
import type { PlanDto } from '../types';

export function PlanPicker({
  plans,
  selectedCode,
  onSelect,
  loading,
}: {
  plans: PlanDto[] | undefined;
  selectedCode: string | null;
  onSelect: (code: string) => void;
  loading?: boolean;
}) {
  if (loading) {
    return (
      <div className="flex justify-center py-8">
        <Spinner />
      </div>
    );
  }
  if (!plans || plans.length === 0) return null;

  return (
    <fieldset className="grid gap-3 sm:grid-cols-3">
      {plans.map((plan) => {
        const selected = plan.code === selectedCode;
        return (
          <label
            key={plan.id}
            className={cn(
              'relative flex cursor-pointer flex-col rounded-2xl border bg-white p-4 shadow-card transition-colors',
              selected ? 'border-brand-500 ring-2 ring-brand-500' : 'border-ink-200 hover:border-brand-300',
            )}
          >
            <input
              type="radio"
              name="plan"
              value={plan.code}
              checked={selected}
              onChange={() => onSelect(plan.code)}
              className="sr-only"
            />
            {selected && (
              <span className="absolute right-3 top-3 flex h-5 w-5 items-center justify-center rounded-full bg-brand-600 text-white">
                <Check className="h-3 w-3" aria-hidden="true" />
              </span>
            )}
            <span className="text-sm font-semibold text-ink-900">{plan.name}</span>
            <span className="mt-1 text-2xl font-bold tabular-nums text-ink-900">{formatInr(plan.price)}</span>
            <ul className="mt-3 space-y-1.5">
              {plan.features.map((f, i) => (
                <li key={i} className="flex items-start gap-1.5 text-xs text-ink-600">
                  <Check className="mt-0.5 h-3 w-3 shrink-0 text-money-600" aria-hidden="true" />
                  {f}
                </li>
              ))}
            </ul>
          </label>
        );
      })}
    </fieldset>
  );
}
