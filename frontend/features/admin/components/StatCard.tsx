import { type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui';
import { cn } from '@/lib/utils';

type Tone = 'brand' | 'money' | 'payable' | 'info' | 'neutral';

const toneStyles: Record<Tone, string> = {
  brand: 'bg-brand-50 text-brand-600',
  money: 'bg-money-50 text-money-700',
  payable: 'bg-payable-50 text-payable-700',
  info: 'bg-sky-50 text-sky-700',
  neutral: 'bg-ink-100 text-ink-600',
};

/**
 * Compact KPI tile for the admin overview/analytics grids: icon chip, big value,
 * label and an optional sub-line. Lightweight (no chart deps).
 */
export function StatCard({
  icon: Icon,
  label,
  value,
  sub,
  tone = 'neutral',
  className,
}: {
  icon?: LucideIcon;
  label: string;
  value: string | number;
  sub?: string;
  tone?: Tone;
  className?: string;
}) {
  return (
    <Card className={cn('p-5', className)}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-medium text-ink-500">{label}</p>
          <p className="mt-1 text-2xl font-semibold tracking-tight text-ink-900">{value}</p>
          {sub && <p className="mt-1 text-xs text-ink-500">{sub}</p>}
        </div>
        {Icon && (
          <span
            className={cn(
              'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl',
              toneStyles[tone],
            )}
          >
            <Icon className="h-5 w-5" aria-hidden="true" />
          </span>
        )}
      </div>
    </Card>
  );
}
