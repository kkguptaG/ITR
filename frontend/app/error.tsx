'use client';

import { useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { ApiError } from '@/lib/api';

/** Route error boundary — maps API problem-details to a friendly message. */
export default function ErrorBoundary({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useTranslations('errors');

  useEffect(() => {
    // Surfaces to the console in dev; a real build would beacon this.
    console.error(error);
  }, [error]);

  const message =
    error instanceof ApiError ? error.problem.detail || error.message : t('generic');
  const correlationId = error instanceof ApiError ? error.correlationId : undefined;

  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-3 px-6 text-center">
      <span className="flex h-12 w-12 items-center justify-center rounded-full bg-red-50 text-red-600">
        <TriangleAlert className="h-6 w-6" aria-hidden="true" />
      </span>
      <h1 className="text-xl font-semibold text-ink-900">{t('pageTitle')}</h1>
      <p className="max-w-md text-sm text-ink-500">{message}</p>
      {correlationId && (
        <p className="text-xs text-ink-400">
          Ref: <span className="font-mono">{correlationId}</span>
        </p>
      )}
      <div className="mt-2">
        <Button onClick={reset}>{t('tryAgain')}</Button>
      </div>
    </div>
  );
}
