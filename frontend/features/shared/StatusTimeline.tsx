'use client';

// ---------------------------------------------------------------------------
// features/shared/StatusTimeline — vertical lifecycle tracker for one return.
//
// Self-contained: maps a ReturnStatus to its position on the canonical filing
// journey (Started → Computed → Paid → Filed → Processed) and renders done /
// current / upcoming steps, plus an error state when a return has Failed.
//
// Robust by design — it takes a plain `status` prop and (optionally) explicit
// step labels. By default it reads labels from the `returns` i18n namespace
// (keys timelineStarted/Computed/Paid/Filed/Processed + timelineFailed, which
// the foundation already ships), so any feature can drop it in. Pass `labels`
// to use a different copy source without coupling to that namespace.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Check, CircleDot, AlertCircle } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ReturnStatus } from '@/lib/api-types';

type StepKey = 'started' | 'computed' | 'paid' | 'filed' | 'processed';

const STEP_ORDER: StepKey[] = ['started', 'computed', 'paid', 'filed', 'processed'];

/** Which timeline step a given status currently sits on. */
const STATUS_STEP: Record<ReturnStatus, StepKey> = {
  Draft: 'started',
  InProgress: 'started',
  ComputedReady: 'computed',
  PendingPayment: 'computed',
  Paid: 'paid',
  UnderCaReview: 'paid',
  ReadyToFile: 'paid',
  Filed: 'filed',
  Processed: 'processed',
  Failed: 'filed', // treated as an e-file failure unless payment-due (handled below)
};

export interface StatusTimelineProps {
  status: ReturnStatus;
  /** Optional label overrides; falls back to the `returns` i18n namespace. */
  labels?: Partial<Record<StepKey, string>>;
  /** Optional failed-step message override. */
  failedLabel?: string;
  className?: string;
}

export function StatusTimeline({ status, labels, failedLabel, className }: StatusTimelineProps) {
  const t = useTranslations('returns');

  const resolved: Record<StepKey, string> = {
    started: labels?.started ?? t('timelineStarted'),
    computed: labels?.computed ?? t('timelineComputed'),
    paid: labels?.paid ?? t('timelinePaid'),
    filed: labels?.filed ?? t('timelineFiled'),
    processed: labels?.processed ?? t('timelineProcessed'),
  };
  const failedText = failedLabel ?? t('timelineFailed');

  const isFailed = status === 'Failed';
  // A payment-due state stalls earlier; otherwise Failed maps to the e-file step.
  const currentStep = status === 'PendingPayment' ? 'computed' : STATUS_STEP[status];
  const currentIndex = STEP_ORDER.indexOf(currentStep);

  return (
    <ol className={cn('space-y-0', className)}>
      {STEP_ORDER.map((step, i) => {
        const isDone = i < currentIndex || status === 'Processed';
        const isCurrent = i === currentIndex && status !== 'Processed';
        const isError = isFailed && isCurrent;
        const isLast = i === STEP_ORDER.length - 1;

        return (
          <li key={step} className="flex gap-3">
            {/* Marker + connector rail */}
            <div className="flex flex-col items-center">
              <span
                className={cn(
                  'flex h-7 w-7 items-center justify-center rounded-full ring-1 ring-inset',
                  isError && 'bg-red-50 text-red-600 ring-red-200',
                  !isError && isDone && 'bg-money-100 text-money-700 ring-money-200',
                  !isError && isCurrent && 'bg-brand-600 text-white ring-brand-600',
                  !isError && !isDone && !isCurrent && 'bg-ink-50 text-ink-400 ring-ink-200',
                )}
                aria-hidden="true"
              >
                {isError ? (
                  <AlertCircle className="h-4 w-4" />
                ) : isDone ? (
                  <Check className="h-4 w-4" />
                ) : isCurrent ? (
                  <CircleDot className="h-4 w-4" />
                ) : (
                  <span className="text-xs font-semibold">{i + 1}</span>
                )}
              </span>
              {!isLast && (
                <span
                  className={cn('my-1 w-px flex-1 grow', isDone ? 'bg-money-300' : 'bg-ink-200')}
                  style={{ minHeight: '1.25rem' }}
                  aria-hidden="true"
                />
              )}
            </div>

            {/* Label */}
            <div className={cn('pb-5', isLast && 'pb-0')}>
              <p
                className={cn(
                  'text-sm font-medium',
                  isError && 'text-red-700',
                  !isError && (isDone || isCurrent) ? 'text-ink-900' : 'text-ink-400',
                )}
              >
                {resolved[step]}
              </p>
              {isError && <p className="mt-0.5 text-xs text-red-600">{failedText}</p>}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
