'use client';

import { Spinner } from '@/components/ui';

/** Suspense fallback rendered inside the auth card while a page's client
 *  search-params resolve. Keeps the card height stable (no layout jump). */
export function AuthFormFallback() {
  return (
    <div className="flex min-h-[18rem] items-center justify-center" aria-busy="true">
      <Spinner />
    </div>
  );
}
