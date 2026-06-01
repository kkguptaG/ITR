'use client';

import { useState, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { NextIntlClientProvider, type AbstractIntlMessages } from 'next-intl';
import { AuthProvider } from '@/lib/auth';
import { ApiError } from '@/lib/api';

export interface ProvidersProps {
  children: ReactNode;
  locale: string;
  messages: AbstractIntlMessages;
  /** Stringified Date from the server (avoids RSC serialization issues). */
  now?: string;
}

/**
 * Client-side provider tree: NextIntl (i18n) > React Query (server state) > Auth.
 * Locale + messages are resolved on the server (root layout) and passed in.
 */
export function Providers({ children, locale, messages, now }: ProvidersProps) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            gcTime: 5 * 60_000,
            refetchOnWindowFocus: false,
            retry: (failureCount, error) => {
              // Don't retry auth/client errors; do retry transient 5xx/network up to 2x.
              if (error instanceof ApiError) {
                if (error.status >= 400 && error.status < 500) return false;
              }
              return failureCount < 2;
            },
          },
          mutations: { retry: 0 },
        },
      }),
  );

  return (
    <NextIntlClientProvider
      locale={locale}
      messages={messages}
      timeZone="Asia/Kolkata"
      now={now ? new Date(now) : undefined}
    >
      <QueryClientProvider client={queryClient}>
        <AuthProvider>{children}</AuthProvider>
      </QueryClientProvider>
    </NextIntlClientProvider>
  );
}
