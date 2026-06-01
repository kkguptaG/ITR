'use client';

// KpiCard — a single dashboard metric (icon, label, value, optional sub-line).
// Tones map to the fintech palette (brand / money-positive / payable-caution).

import { type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui';
import { cn } from '@/lib/utils';

type Tone = 'brand' | 'money' | 'payable' | 'neutral';

const toneStyles: Record<Tone, { icon: string }> = {
  brand: { icon: 'bg-brand-50 text-brand-600' },
  money: { icon: 'bg-money-50 text-money-600' },
  payable: { icon: 'bg-payable-50 text-payable-600' },
  neutral: { icon: 'bg-ink-100 text-ink-600' },
};

export interface KpiCardProps {
  icon: LucideIcon;
  label: string;
  value: string;
  sub?: string;
  tone?: Tone;
}

export function KpiCard({ icon: Icon, label, value, sub, tone = 'neutral' }: KpiCardProps) {
  return (
    <Card className="p-5">
      <div className="flex items-start gap-4">
        <span
          className={cn(
            'flex h-11 w-11 shrink-0 items-center justify-center rounded-xl',
            toneStyles[tone].icon,
          )}
          aria-hidden="true"
        >
          <Icon className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <p className="text-sm text-ink-500">{label}</p>
          <p className="mt-0.5 truncate text-2xl font-semibold tabular-nums text-ink-900">
            {value}
          </p>
          {sub && <p className="mt-0.5 truncate text-xs text-ink-500">{sub}</p>}
        </div>
      </div>
    </Card>
  );
}
