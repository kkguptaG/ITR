'use client';

import { type ReactNode } from 'react';

/**
 * Consistent title/subtitle block at the top of each auth form panel.
 * Pure presentational; styling matches the foundation design system.
 */
export function AuthHeading({
  title,
  subtitle,
}: {
  title: ReactNode;
  subtitle?: ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <h1 className="text-xl font-semibold tracking-tight text-ink-900">{title}</h1>
      {subtitle && <p className="text-sm text-ink-500">{subtitle}</p>}
    </div>
  );
}
