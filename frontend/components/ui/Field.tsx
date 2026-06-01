'use client';

import { type ReactNode, isValidElement, cloneElement } from 'react';
import { Label } from './Label';
import { cn } from '@/lib/utils';
import { genId } from '@/lib/utils';

export interface FieldProps {
  label?: ReactNode;
  htmlFor?: string;
  required?: boolean;
  /** Helper text shown below the control. */
  hint?: ReactNode;
  /** Error message; when present, control is marked invalid + aria-describedby. */
  error?: string | null;
  className?: string;
  children: ReactNode;
}

/**
 * Field wraps a control with its label, helper text and error message and wires
 * up the a11y contract (label htmlFor, aria-describedby, aria-invalid). It will
 * inject `id`, `aria-invalid` and `aria-describedby` onto a single child input
 * when an explicit `htmlFor` isn't supplied.
 */
export function Field({ label, htmlFor, required, hint, error, className, children }: FieldProps) {
  const controlId = htmlFor ?? genId('field');
  const hintId = hint ? `${controlId}-hint` : undefined;
  const errorId = error ? `${controlId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

  // Best-effort: enrich a single child control with id + aria attributes.
  let control = children;
  if (isValidElement(children) && !htmlFor) {
    control = cloneElement(children as React.ReactElement, {
      id: (children.props as { id?: string }).id ?? controlId,
      'aria-describedby': describedBy,
      'aria-invalid': error ? true : (children.props as { 'aria-invalid'?: boolean })['aria-invalid'],
      invalid: error ? true : (children.props as { invalid?: boolean }).invalid,
    });
  }

  return (
    <div className={cn('space-y-1.5', className)}>
      {label && (
        <Label htmlFor={controlId} required={required}>
          {label}
        </Label>
      )}
      {control}
      {hint && !error && (
        <p id={hintId} className="text-xs text-ink-500">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-xs font-medium text-red-600" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
