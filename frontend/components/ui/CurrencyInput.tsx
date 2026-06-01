'use client';

import { forwardRef, useEffect, useState, type InputHTMLAttributes } from 'react';
import { cn } from '@/lib/utils';
import { formatNumber, parseInr } from '@/lib/format';
import { inputBaseClass } from './Input';

export interface CurrencyInputProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> {
  /** Numeric value (rupees). Engine wants clean NUMERIC(14,2)-aligned decimals. */
  value?: number | null;
  /** Called with the parsed number (or null when cleared). */
  onValueChange?: (value: number | null) => void;
  invalid?: boolean;
}

/**
 * CurrencyInput — shows ₹ prefix and Indian lakh/crore grouping (1,23,456)
 * while keeping a clean numeric value for the tax engine. Controlled via
 * `value`/`onValueChange`; designed to bind to react-hook-form's Controller.
 */
export const CurrencyInput = forwardRef<HTMLInputElement, CurrencyInputProps>(
  function CurrencyInput({ value, onValueChange, invalid, className, onBlur, ...props }, ref) {
    const [display, setDisplay] = useState<string>(
      value === null || value === undefined ? '' : formatNumber(value),
    );

    // Re-sync display when the external value changes (e.g. autosave / reset).
    useEffect(() => {
      if (value === null || value === undefined) {
        setDisplay('');
      } else {
        setDisplay(formatNumber(value));
      }
    }, [value]);

    return (
      <div className="relative">
        <span
          className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-sm text-ink-500"
          aria-hidden="true"
        >
          ₹
        </span>
        <input
          ref={ref}
          inputMode="decimal"
          aria-invalid={invalid || undefined}
          className={cn(
            inputBaseClass,
            'pl-7 tabular-nums',
            invalid ? 'border-red-400 focus:ring-red-500 focus:border-red-500' : 'border-ink-300',
            className,
          )}
          value={display}
          onChange={(e) => {
            const raw = e.target.value;
            // Allow only digits, comma, dot while typing.
            const cleaned = raw.replace(/[^\d.,]/g, '');
            setDisplay(cleaned);
            if (cleaned.trim() === '') {
              onValueChange?.(null);
            } else {
              onValueChange?.(parseInr(cleaned));
            }
          }}
          onBlur={(e) => {
            // Normalize to grouped form on blur.
            const n = parseInr(display);
            setDisplay(display.trim() === '' ? '' : formatNumber(n));
            onBlur?.(e);
          }}
          {...props}
        />
      </div>
    );
  },
);
