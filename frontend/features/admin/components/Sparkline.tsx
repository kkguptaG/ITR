'use client';

import { useId } from 'react';
import { cn } from '@/lib/utils';

/**
 * Tiny inline SVG line/area chart for a revenue trend. Dependency-free.
 * Renders a smooth polyline with a subtle gradient fill; scales the series to
 * the viewBox. Falls back to a flat baseline when all values are equal/zero.
 */
export function Sparkline({
  values,
  width = 560,
  height = 120,
  strokeClassName = 'stroke-brand-500',
  fillId,
  className,
  ariaLabel = 'Trend',
}: {
  values: number[];
  width?: number;
  height?: number;
  strokeClassName?: string;
  fillId?: string;
  className?: string;
  ariaLabel?: string;
}) {
  const autoId = useId();
  const gradId = fillId ?? `spark-${autoId}`;
  const pad = 6;

  if (values.length === 0) {
    return (
      <div
        className={cn('flex h-[120px] items-center justify-center text-sm text-ink-400', className)}
      >
        No data
      </div>
    );
  }

  const max = Math.max(...values);
  const min = Math.min(...values);
  const range = max - min || 1;
  const innerW = width - pad * 2;
  const innerH = height - pad * 2;

  const points = values.map((v, i) => {
    const x = values.length === 1 ? width / 2 : pad + (i / (values.length - 1)) * innerW;
    const y = pad + innerH - ((v - min) / range) * innerH;
    return [x, y] as const;
  });

  const line = points.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
  const area =
    `M${points[0][0].toFixed(1)},${(height - pad).toFixed(1)} ` +
    points.map(([x, y]) => `L${x.toFixed(1)},${y.toFixed(1)}`).join(' ') +
    ` L${points[points.length - 1][0].toFixed(1)},${(height - pad).toFixed(1)} Z`;

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      className={cn('w-full', className)}
      role="img"
      aria-label={ariaLabel}
      preserveAspectRatio="none"
    >
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" className="text-brand-500" stopColor="currentColor" stopOpacity="0.18" />
          <stop offset="100%" className="text-brand-500" stopColor="currentColor" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${gradId})`} stroke="none" />
      <path
        d={line}
        fill="none"
        className={strokeClassName}
        strokeWidth={2}
        strokeLinejoin="round"
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
      {points.map(([x, y], i) => (
        <circle key={i} cx={x} cy={y} r={2.5} className="fill-brand-600" />
      ))}
    </svg>
  );
}
