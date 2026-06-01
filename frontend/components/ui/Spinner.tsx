import { Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface SpinnerProps {
  className?: string;
  size?: number;
  label?: string;
}

/** Accessible loading spinner. Provide `label` for standalone use. */
export function Spinner({ className, size = 20, label }: SpinnerProps) {
  return (
    <span role="status" className={cn('inline-flex items-center gap-2 text-ink-500', className)}>
      <Loader2 className="animate-spin" style={{ width: size, height: size }} aria-hidden="true" />
      {label ? <span className="text-sm">{label}</span> : <span className="sr-only">Loading</span>}
    </span>
  );
}
