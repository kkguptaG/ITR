'use client';

import { forwardRef, useId, type InputHTMLAttributes } from 'react';
import type { LucideIcon } from 'lucide-react';
import { Field, Input } from '@/components/ui';

export interface IconFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  /** Leading icon rendered inside the input. */
  icon: LucideIcon;
  required?: boolean;
  hint?: string;
  error?: string | null;
}

/**
 * A labelled text input with a leading icon, wired for accessibility.
 *
 * We pass an explicit `htmlFor`/`id` so <Field> associates the <label> with the
 * real <input> (not the icon wrapper) and we set `invalid` on the Input
 * ourselves. Designed to be spread with react-hook-form's `register(...)`.
 */
export const IconField = forwardRef<HTMLInputElement, IconFieldProps>(function IconField(
  { label, icon: Icon, required, hint, error, className, id, ...inputProps },
  ref,
) {
  const reactId = useId();
  const controlId = id ?? `f-${reactId}`;

  return (
    <Field label={label} htmlFor={controlId} required={required} hint={hint} error={error}>
      <div className="relative">
        <Icon
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400"
          aria-hidden="true"
        />
        <Input
          ref={ref}
          {...inputProps}
          id={controlId}
          invalid={!!error}
          className={['pl-9', className].filter(Boolean).join(' ')}
        />
      </div>
    </Field>
  );
});
