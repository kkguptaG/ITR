'use client';

import { forwardRef, type InputHTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export const inputBaseClass =
  'block w-full rounded-xl border bg-white px-3.5 text-ink-900 placeholder:text-ink-400 ' +
  'shadow-sm transition-colors h-11 text-sm ' +
  'focus:outline-none focus:ring-2 focus:ring-brand-500 focus:border-brand-500 ' +
  'disabled:cursor-not-allowed disabled:bg-ink-50 disabled:text-ink-400';

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { className, invalid, ...props },
  ref,
) {
  return (
    <input
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(
        inputBaseClass,
        invalid ? 'border-red-400 focus:ring-red-500 focus:border-red-500' : 'border-ink-300',
        className,
      )}
      {...props}
    />
  );
});
