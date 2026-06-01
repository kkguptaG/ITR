import { type ReactNode } from 'react';
import { AlertCircle, CheckCircle2, Info, TriangleAlert } from 'lucide-react';
import { cn } from '@/lib/utils';

type Variant = 'info' | 'success' | 'warning' | 'error';

export interface AlertProps {
  variant?: Variant;
  title?: ReactNode;
  children?: ReactNode;
  className?: string;
}

const styles: Record<Variant, { wrap: string; icon: typeof Info }> = {
  info: { wrap: 'border-sky-200 bg-sky-50 text-sky-900', icon: Info },
  success: { wrap: 'border-money-200 bg-money-50 text-money-900', icon: CheckCircle2 },
  warning: { wrap: 'border-payable-200 bg-payable-50 text-payable-800', icon: TriangleAlert },
  error: { wrap: 'border-red-200 bg-red-50 text-red-900', icon: AlertCircle },
};

/** Inline status banner. Errors use role=alert for assistive announcement. */
export function Alert({ variant = 'info', title, children, className }: AlertProps) {
  const { wrap, icon: Icon } = styles[variant];
  return (
    <div
      role={variant === 'error' ? 'alert' : 'status'}
      className={cn('flex gap-3 rounded-xl border p-3.5 text-sm', wrap, className)}
    >
      <Icon className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
      <div className="space-y-0.5">
        {title && <p className="font-semibold">{title}</p>}
        {children && <div className="text-sm/relaxed opacity-90">{children}</div>}
      </div>
    </div>
  );
}
