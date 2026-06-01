'use client';

// ---------------------------------------------------------------------------
// Switch — small accessible toggle (role=switch). Local to the settings feature
// to avoid touching the foundation-owned components/ui barrel. Keyboard + ARIA
// correct: Space/Enter toggles, aria-checked reflects state.
// ---------------------------------------------------------------------------

import { cn } from '@/lib/utils';

export interface SwitchProps {
  checked: boolean;
  onChange: (next: boolean) => void;
  disabled?: boolean;
  /** Accessible label (use when there is no visible <label> association). */
  label?: string;
  id?: string;
  className?: string;
}

export function Switch({ checked, onChange, disabled, label, id, className }: SwitchProps) {
  return (
    <button
      type="button"
      role="switch"
      id={id}
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => !disabled && onChange(!checked)}
      className={cn(
        'relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2',
        checked ? 'bg-brand-600' : 'bg-ink-300',
        disabled && 'cursor-not-allowed opacity-50',
        className,
      )}
    >
      <span
        className={cn(
          'inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform',
          checked ? 'translate-x-5' : 'translate-x-0.5',
        )}
        aria-hidden="true"
      />
    </button>
  );
}
