'use client';

// ---------------------------------------------------------------------------
// StatusTimeline — vertical lifecycle tracker for a single return.
// Maps the ReturnStatus to a position along the canonical filing journey
// (Draft → In progress → Computed → Payment → Filed → Processed) and renders
// done / current / upcoming steps. A Failed return surfaces an error state at
// the step it stalled on (payment vs e-file).
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { Check, CircleDot, AlertCircle } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ReturnStatus } from '@/lib/api-types';

type StepKey = 'started' | 'computed' | 'paid' | 'filed' | 'everified' | 'processed';

const STEP_ORDER: StepKey[] = ['started', 'computed', 'paid', 'filed', 'everified', 'processed'];

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
  Failed: 'filed', // stalled at the e-file step (unless payment-due, handled below)
};

export function StatusTimeline({ status, eVerified = false }: { status: ReturnStatus; eVerified?: boolean }) {
  const t = useTranslations('returns');

  const labels: Record<StepKey, string> = {
    started: t('timelineStarted'),
    computed: t('timelineComputed'),
    paid: t('timelinePaid'),
    filed: t('timelineFiled'),
    everified: t('timelineEverified'),
    processed: t('timelineProcessed'),
  };

  const isFailed = status === 'Failed';
  // The current (in-progress) step. A payment-due failure stalls at computed; a Filed return's next
  // action is e-verification (then CPC processing) — so it advances past "filed" only once verified.
  const currentStep: StepKey =
    status === 'PendingPayment' ? 'computed'
      : status === 'Filed' ? (eVerified ? 'processed' : 'everified')
      : STATUS_STEP[status];
  const currentIndex = STEP_ORDER.indexOf(currentStep);

  return (
    <ol className="space-y-0">
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
                  className={cn(
                    'my-1 w-px flex-1 grow',
                    isDone ? 'bg-money-300' : 'bg-ink-200',
                  )}
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
                {labels[step]}
              </p>
              {isError && (
                <p className="mt-0.5 text-xs text-red-600">{t('timelineFailed')}</p>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
