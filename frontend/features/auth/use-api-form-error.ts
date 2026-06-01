// ---------------------------------------------------------------------------
// features/auth/use-api-form-error.ts
// Maps an ApiError (RFC 7807 problem+json) onto a react-hook-form instance:
//   • field-level errors -> setError(field, ...)   (highlights the input)
//   • everything else     -> a single form-level banner message
//
// Field names from the backend are camelCase (matching the DTOs / form keys),
// so they map 1:1 onto the form fields. Unknown fields fall back to the banner.
// ---------------------------------------------------------------------------

import { useCallback, useState } from 'react';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { ApiError } from '@/lib/api';

export interface UseApiFormError<T extends FieldValues> {
  /** Form-level message to render in an <Alert variant="error">, or null. */
  formError: string | null;
  /** Apply an unknown error to the form (field errors + banner fallback). */
  handleError: (error: unknown, knownFields?: Array<Path<T>>) => void;
  /** Clear the banner (call on a fresh submit). */
  reset: () => void;
}

export function useApiFormError<T extends FieldValues>(
  setError: UseFormSetError<T>,
): UseApiFormError<T> {
  const [formError, setFormError] = useState<string | null>(null);

  const handleError = useCallback(
    (error: unknown, knownFields: Array<Path<T>> = []) => {
      if (error instanceof ApiError) {
        const fieldErrors = error.problem.errors ?? [];
        let appliedToField = false;

        for (const fe of fieldErrors) {
          const field = fe.field as Path<T>;
          if (knownFields.includes(field)) {
            setError(field, { type: 'server', message: fe.message });
            appliedToField = true;
          }
        }

        // If nothing matched a known field, surface a banner. Prefer the most
        // specific message available.
        if (!appliedToField) {
          setFormError(error.firstFieldError ?? error.problem.detail ?? error.message);
        } else {
          setFormError(null);
        }
        return;
      }

      setFormError(
        error instanceof Error ? error.message : 'Something went wrong. Please try again.',
      );
    },
    [setError],
  );

  const reset = useCallback(() => setFormError(null), []);

  return { formError, handleError, reset };
}
