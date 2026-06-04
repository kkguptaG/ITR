import { type ReactNode } from 'react';
import { cn } from '@/lib/utils';

export interface ProgressRingProps {
  /** 0–100. */
  value: number;
  size?: number;
  stroke?: number;
  /** Tailwind text-color class for the progress arc (defaults to brand). */
  colorClass?: string;
  trackClass?: string;
  children?: ReactNode;
  className?: string;
}

/**
 * A circular progress ring (SVG) with centred content. Used for the dashboard
 * filing-status gauge. Pure/presentational; colour via Tailwind text-* classes.
 */
export function ProgressRing({
  value,
  size = 96,
  stroke = 9,
  colorClass = 'text-brand-600',
  trackClass = 'text-ink-100',
  children,
  className,
}: ProgressRingProps) {
  const clamped = Math.max(0, Math.min(100, value));
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const offset = c * (1 - clamped / 100);

  return (
    <div className={cn('relative inline-flex items-center justify-center', className)} style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90" aria-hidden="true">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          strokeWidth={stroke}
          className={trackClass}
          stroke="currentColor"
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          strokeWidth={stroke}
          strokeLinecap="round"
          className={cn('transition-[stroke-dashoffset] duration-700', colorClass)}
          stroke="currentColor"
          strokeDasharray={c}
          strokeDashoffset={offset}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">{children}</div>
    </div>
  );
}
