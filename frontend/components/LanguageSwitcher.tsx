'use client';

import { useLocale } from 'next-intl';
import { useRouter } from 'next/navigation';
import { useTransition } from 'react';
import { Languages } from 'lucide-react';
import { cn } from '@/lib/utils';
import { locales, LOCALE_COOKIE, type Locale } from '@/i18n-config';

const labels: Record<Locale, string> = {
  en: 'EN',
  hi: 'हिं',
};

/**
 * Toggles the active locale by writing the locale cookie and refreshing the
 * server tree (next-intl reads the cookie in i18n.ts). Locale-agnostic routing
 * means no URL change — just a re-render with the new message catalog.
 */
export function LanguageSwitcher({ className }: { className?: string }) {
  const active = useLocale() as Locale;
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  function select(locale: Locale) {
    if (locale === active) return;
    // 1 year, site-wide cookie.
    document.cookie = `${LOCALE_COOKIE}=${locale}; path=/; max-age=31536000; samesite=lax`;
    startTransition(() => {
      router.refresh();
    });
  }

  return (
    <div
      className={cn('inline-flex items-center gap-0.5 rounded-lg bg-ink-100 p-0.5', className)}
      role="group"
      aria-label="Language"
    >
      <Languages className="mx-1 h-4 w-4 text-ink-400" aria-hidden="true" />
      {locales.map((loc) => (
        <button
          key={loc}
          type="button"
          onClick={() => select(loc)}
          disabled={pending}
          aria-pressed={active === loc}
          className={cn(
            'min-w-[2.25rem] rounded-md px-2 py-1 text-xs font-semibold transition-colors focus-visible:ring-2 focus-visible:ring-brand-500',
            active === loc ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500 hover:text-ink-800',
          )}
        >
          {labels[loc]}
        </button>
      ))}
    </div>
  );
}
