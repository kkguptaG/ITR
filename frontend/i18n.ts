// ---------------------------------------------------------------------------
// i18n.ts — next-intl request config (locale-agnostic, single-segment demo).
//
// The docs describe a [locale] route segment for production; for this
// build-reliable foundation we keep routes clean and negotiate the active
// locale from a cookie (set by the LanguageSwitcher), defaulting to "en".
// Adding the [locale] segment later is a routing change, not a content change.
// ---------------------------------------------------------------------------

import { getRequestConfig } from 'next-intl/server';
import { cookies } from 'next/headers';
import { LOCALE_COOKIE, defaultLocale, isLocale, type Locale } from './i18n-config';

// Re-export the client-safe constants so existing `@/i18n` importers keep working.
export { locales, defaultLocale, LOCALE_COOKIE, isLocale } from './i18n-config';
export type { Locale } from './i18n-config';

export default getRequestConfig(async () => {
  const cookieLocale = cookies().get(LOCALE_COOKIE)?.value;
  const locale: Locale = isLocale(cookieLocale) ? cookieLocale : defaultLocale;

  const messages = (await import(`./messages/${locale}.json`)).default;

  return {
    locale,
    messages,
    // Indian grouping for both locales; only script/words differ.
    formats: {
      number: {
        inr: { style: 'currency', currency: 'INR' },
      },
    },
    now: new Date(),
    timeZone: 'Asia/Kolkata',
  };
});
