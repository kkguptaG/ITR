'use client';

import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface StepperStep {
  /** Stable key/id (e.g. route step slug). */
  key: string;
  label: string;
}

export interface StepperProps {
  steps: StepperStep[];
  /** Zero-based index of the active step. */
  current: number;
  className?: string;
  /** Optional click handler for already-completed steps (back-nav). */
  onStepClick?: (index: number, step: StepperStep) => void;
}

/**
 * WizardStepper — horizontal on desktop (numbered nodes + connectors), compact
 * "Step n of m · p%" on mobile. Done = check, active = filled, locked = muted.
 * Render inside a sticky container for the filing wizard (doc 8.3.2).
 */
export function Stepper({ steps, current, className, onStepClick }: StepperProps) {
  const total = steps.length;
  const percent = total > 0 ? Math.round(((current + 1) / total) * 100) : 0;
  const safeCurrent = Math.min(Math.max(current, 0), Math.max(total - 1, 0));

  return (
    <nav aria-label="Progress" className={cn('w-full', className)}>
      {/* Mobile: compact dots + label */}
      <div className="md:hidden">
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium text-ink-900">{steps[safeCurrent]?.label}</span>
          <span className="text-ink-500" aria-live="polite">
            Step {safeCurrent + 1} of {total} · {percent}%
          </span>
        </div>
        <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-ink-200" role="presentation">
          <div
            className="h-full rounded-full bg-brand-600 transition-all"
            style={{ width: `${percent}%` }}
          />
        </div>
      </div>

      {/* Desktop: full horizontal stepper */}
      <ol className="hidden items-center md:flex">
        {steps.map((step, i) => {
          const isComplete = i < safeCurrent;
          const isActive = i === safeCurrent;
          const isClickable = isComplete && !!onStepClick;
          return (
            <li key={step.key} className={cn('flex items-center', i < total - 1 && 'flex-1')}>
              <button
                type="button"
                disabled={!isClickable}
                onClick={isClickable ? () => onStepClick?.(i, step) : undefined}
                aria-current={isActive ? 'step' : undefined}
                className={cn(
                  'flex items-center gap-2 rounded-lg px-1.5 py-1 text-left',
                  isClickable && 'hover:bg-ink-50 focus-visible:ring-2 focus-visible:ring-brand-500',
                  !isClickable && 'cursor-default',
                )}
              >
                <span
                  className={cn(
                    'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold ring-1 ring-inset transition-colors',
                    isComplete && 'bg-brand-600 text-white ring-brand-600',
                    isActive && 'bg-brand-50 text-brand-700 ring-brand-500',
                    !isComplete && !isActive && 'bg-white text-ink-400 ring-ink-300',
                  )}
                >
                  {isComplete ? <Check className="h-4 w-4" aria-hidden="true" /> : i + 1}
                </span>
                <span
                  className={cn(
                    'whitespace-nowrap text-sm',
                    isActive ? 'font-medium text-ink-900' : 'text-ink-500',
                  )}
                >
                  {step.label}
                </span>
              </button>
              {i < total - 1 && (
                <span
                  aria-hidden="true"
                  className={cn(
                    'mx-2 h-px flex-1 transition-colors',
                    i < safeCurrent ? 'bg-brand-500' : 'bg-ink-200',
                  )}
                />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
