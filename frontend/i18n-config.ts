// ---------------------------------------------------------------------------
// i18n-config.ts — CLIENT-SAFE locale constants/types (NO next/headers).
// Shared by both the server request-config (i18n.ts) and client components
// (e.g. LanguageSwitcher), so client bundles never pull server-only APIs.
// ---------------------------------------------------------------------------

export const locales = ['en', 'hi'] as const;
export type Locale = (typeof locales)[number];
export const defaultLocale: Locale = 'en';
export const LOCALE_COOKIE = 'tallyg.locale';

export function isLocale(value: string | undefined | null): value is Locale {
  return !!value && (locales as readonly string[]).includes(value);
}
