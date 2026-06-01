'use client';

import { cn } from '@/lib/utils';

export interface BarDatum {
  label: string;
  value: number;
  /** Optional secondary label rendered under the value (e.g. count). */
  hint?: string;
}

/**
 * Dependency-free horizontal bar chart (CSS widths). Used for revenue-by-period
 * and funnel breakdowns so we don't pull in a heavy chart library. Values are
 * normalised to the largest bar; `format` renders the numeric label.
 */
export function BarChart({
  data,
  format = (v) => String(v),
  barClassName = 'bg-brand-500',
  emptyLabel = 'No data',
  className,
}: {
  data: BarDatum[];
  format?: (value: number) => string;
  barClassName?: string;
  emptyLabel?: string;
  className?: string;
}) {
  const max = data.reduce((m, d) => Math.max(m, d.value), 0);

  if (data.length === 0) {
    return <p className="py-6 text-center text-sm text-ink-400">{emptyLabel}</p>;
  }

  return (
    <ul className={cn('space-y-3', className)}>
      {data.map((d) => {
        const pct = max > 0 ? Math.max(2, Math.round((d.value / max) * 100)) : 0;
        return (
          <li key={d.label} className="space-y-1">
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span className="truncate font-medium text-ink-700">{d.label}</span>
              <span className="shrink-0 tabular-nums text-ink-900">
                {format(d.value)}
                {d.hint && <span className="ml-1 text-xs text-ink-400">{d.hint}</span>}
              </span>
            </div>
            <div className="h-2.5 w-full overflow-hidden rounded-full bg-ink-100">
              <div
                className={cn('h-full rounded-full transition-all', barClassName)}
                style={{ width: `${pct}%` }}
                role="presentation"
              />
            </div>
          </li>
        );
      })}
    </ul>
  );
}
