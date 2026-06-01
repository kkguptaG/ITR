import { type HTMLAttributes } from 'react';
import { cn } from '@/lib/utils';
import type { ReturnStatus } from '@/lib/api-types';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: Tone;
}

const tones: Record<Tone, string> = {
  neutral: 'bg-ink-100 text-ink-700 ring-ink-200',
  brand: 'bg-brand-50 text-brand-700 ring-brand-200',
  success: 'bg-money-50 text-money-700 ring-money-200',
  warning: 'bg-payable-50 text-payable-700 ring-payable-200',
  danger: 'bg-red-50 text-red-700 ring-red-200',
  info: 'bg-sky-50 text-sky-700 ring-sky-200',
};

export function Badge({ className, tone = 'neutral', ...props }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset',
        tones[tone],
        className,
      )}
      {...props}
    />
  );
}

/** Map of ReturnStatus -> badge tone (consistent across the app). */
export const statusTone: Record<ReturnStatus, Tone> = {
  Draft: 'neutral',
  InProgress: 'info',
  ComputedReady: 'brand',
  PendingPayment: 'warning',
  Paid: 'brand',
  UnderCaReview: 'info',
  ReadyToFile: 'brand',
  Filed: 'success',
  Processed: 'success',
  Failed: 'danger',
};

/**
 * StatusBadge renders a ReturnStatus with a consistent color + the given label.
 * Pass the already-translated label (e.g. from next-intl) as children.
 */
export function StatusBadge({
  status,
  children,
  className,
}: {
  status: ReturnStatus;
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <Badge tone={statusTone[status]} className={className}>
      {children ?? status}
    </Badge>
  );
}
