'use client';

import {
  useRef,
  useMemo,
  type ClipboardEvent,
  type KeyboardEvent,
} from 'react';
import { cn } from '@/lib/utils';

export interface OtpInputProps {
  /** Current value (e.g. "1234"). */
  value: string;
  onChange: (value: string) => void;
  length?: number;
  /** Fired when all boxes are filled. */
  onComplete?: (value: string) => void;
  disabled?: boolean;
  invalid?: boolean;
  autoFocus?: boolean;
  'aria-label'?: string;
}

/**
 * OtpInput — `length` segmented single-digit boxes with auto-advance,
 * backspace-to-previous, and full paste-fill. Numeric only.
 */
export function OtpInput({
  value,
  onChange,
  length = 6,
  onComplete,
  disabled,
  invalid,
  autoFocus,
  'aria-label': ariaLabel = 'One-time code',
}: OtpInputProps) {
  const inputs = useRef<Array<HTMLInputElement | null>>([]);
  const digits = useMemo(() => {
    const arr = value.split('').slice(0, length);
    while (arr.length < length) arr.push('');
    return arr;
  }, [value, length]);

  const emit = (next: string) => {
    onChange(next);
    if (next.length === length && !next.includes(' ') && next.replace(/\D/g, '').length === length) {
      onComplete?.(next);
    }
  };

  const setDigit = (index: number, digit: string) => {
    const arr = digits.slice();
    arr[index] = digit;
    emit(arr.join('').replace(/\s/g, '').slice(0, length));
  };

  const handleChange = (index: number, raw: string) => {
    const onlyDigits = raw.replace(/\D/g, '');
    if (!onlyDigits) {
      setDigit(index, '');
      return;
    }
    // If multiple chars (autofill), distribute across boxes from here.
    if (onlyDigits.length > 1) {
      const arr = digits.slice();
      let cursor = index;
      for (const ch of onlyDigits.split('')) {
        if (cursor >= length) break;
        arr[cursor] = ch;
        cursor += 1;
      }
      emit(arr.join('').slice(0, length));
      inputs.current[Math.min(cursor, length - 1)]?.focus();
      return;
    }
    setDigit(index, onlyDigits);
    if (index < length - 1) inputs.current[index + 1]?.focus();
  };

  const handleKeyDown = (index: number, e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace') {
      if (digits[index]) {
        setDigit(index, '');
      } else if (index > 0) {
        inputs.current[index - 1]?.focus();
        setDigit(index - 1, '');
      }
    } else if (e.key === 'ArrowLeft' && index > 0) {
      inputs.current[index - 1]?.focus();
    } else if (e.key === 'ArrowRight' && index < length - 1) {
      inputs.current[index + 1]?.focus();
    }
  };

  const handlePaste = (e: ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, length);
    if (pasted) {
      emit(pasted);
      inputs.current[Math.min(pasted.length, length - 1)]?.focus();
    }
  };

  return (
    <div role="group" aria-label={ariaLabel} className="flex gap-2">
      {digits.map((digit, i) => (
        <input
          key={i}
          ref={(el) => {
            inputs.current[i] = el;
          }}
          type="text"
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          maxLength={1}
          autoFocus={autoFocus && i === 0}
          disabled={disabled}
          aria-label={`${ariaLabel} digit ${i + 1}`}
          aria-invalid={invalid || undefined}
          value={digit}
          onChange={(e) => handleChange(i, e.target.value)}
          onKeyDown={(e) => handleKeyDown(i, e)}
          onPaste={handlePaste}
          onFocus={(e) => e.target.select()}
          className={cn(
            'h-12 w-11 rounded-xl border bg-white text-center text-lg font-semibold text-ink-900 shadow-sm transition-colors tabular-nums',
            'focus:outline-none focus:ring-2 focus:ring-brand-500 focus:border-brand-500',
            'disabled:cursor-not-allowed disabled:bg-ink-50',
            invalid ? 'border-red-400 focus:ring-red-500' : 'border-ink-300',
          )}
        />
      ))}
    </div>
  );
}
