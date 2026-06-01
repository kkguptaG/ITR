'use client';

// ---------------------------------------------------------------------------
// LanguagePreferenceCard — choose the app language (EN / हिंदी).
//   • Sets the locale cookie (next-intl reads it in i18n.ts) and refreshes the
//     server tree — same mechanism as the Topbar LanguageSwitcher, no URL change.
//   • Best-effort syncs preferredLanguage to the server (PATCH /me) so the
//     choice follows the user across devices; a sync failure is non-fatal.
// ---------------------------------------------------------------------------

import { useTransition } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { Check, Languages } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui';
import { cn } from '@/lib/utils';
import { locales, LOCALE_COOKIE, type Locale } from '@/i18n-config';
import { updateProfile } from '../api';

const NATIVE: Record<Locale, string> = { en: 'English', hi: 'हिंदी' };
const SUB: Record<Locale, string> = { en: 'English', hi: 'Hindi' };

export function LanguagePreferenceCard() {
  const t = useTranslations('settings');
  const active = useLocale() as Locale;
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  function select(locale: Locale) {
    if (locale === active) return;
    document.cookie = `${LOCALE_COOKIE}=${locale}; path=/; max-age=31536000; samesite=lax`;
    // Best-effort cross-device sync; never block the UI on it.
    updateProfile({ preferredLanguage: locale }).catch(() => {
      /* preference still applied locally via cookie */
    });
    startTransition(() => router.refresh());
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Languages className="h-5 w-5 text-brand-600" aria-hidden="true" />
          {t('languageTitle')}
        </CardTitle>
        <CardDescription>{t('languageSubtitle')}</CardDescription>
      </CardHeader>
      <CardContent>
        <div
          role="radiogroup"
          aria-label={t('languageTitle')}
          className="grid grid-cols-1 gap-3 sm:grid-cols-2"
        >
          {locales.map((loc) => {
            const selected = loc === active;
            return (
              <button
                key={loc}
                type="button"
                role="radio"
                aria-checked={selected}
                disabled={pending}
                onClick={() => select(loc)}
                className={cn(
                  'flex items-center justify-between rounded-xl border px-4 py-3 text-left transition-colors',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
                  selected
                    ? 'border-brand-400 bg-brand-50/60 ring-1 ring-brand-300'
                    : 'border-ink-200 bg-white hover:bg-ink-50',
                  pending && 'opacity-70',
                )}
              >
                <span>
                  <span className="block text-sm font-semibold text-ink-900">{NATIVE[loc]}</span>
                  <span className="block text-xs text-ink-500">{SUB[loc]}</span>
                </span>
                {selected && <Check className="h-5 w-5 text-brand-600" aria-hidden="true" />}
              </button>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}
