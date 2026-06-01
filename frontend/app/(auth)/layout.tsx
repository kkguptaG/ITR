import Link from 'next/link';
import { ShieldCheck, Lock, MapPin, BadgeCheck, Wand2, Scale } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';

/**
 * Split-screen auth shell for /login, /register, /verify-otp.
 *
 *  • Left  (lg+): a fintech "trust" panel — brand, value props and the
 *    encryption / data-residency / DPDP / CA-verified assurances.
 *  • Right (all sizes): a centered white card that renders the feature page.
 *
 * The trust panel collapses on mobile; a compact trust footer keeps the
 * assurances visible on small screens. Feature pages only render their form.
 */
export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  const t = await getTranslations('common');
  const tl = await getTranslations('landing');

  const trustPoints = [
    { key: 'trustEncryption', Icon: Lock },
    { key: 'trustDataIndia', Icon: MapPin },
    { key: 'trustDpdp', Icon: ShieldCheck },
    { key: 'trustCaVerified', Icon: BadgeCheck },
  ] as const;

  const valueProps = [
    { titleKey: 'featureWizardTitle', bodyKey: 'featureWizardBody', Icon: Wand2 },
    { titleKey: 'featureRegimeTitle', bodyKey: 'featureRegimeBody', Icon: Scale },
  ] as const;

  return (
    <div className="flex min-h-screen flex-col bg-ink-50 lg:flex-row">
      {/* ---- Left: trust / brand panel (lg+) -------------------------------- */}
      <aside className="relative hidden overflow-hidden bg-brand-700 lg:flex lg:w-[44%] lg:flex-col xl:w-2/5">
        <div className="absolute inset-0 bg-hero-gradient opacity-70" aria-hidden="true" />
        <div className="bg-grid absolute inset-0 opacity-40" aria-hidden="true" />

        <div className="relative z-10 flex h-full flex-col justify-between p-10 xl:p-12">
          <Link href="/" className="flex items-center gap-2 text-white">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/15 backdrop-blur">
              <ShieldCheck className="h-5 w-5" aria-hidden="true" />
            </span>
            <span className="text-lg font-semibold">{t('appName')}</span>
          </Link>

          <div className="max-w-md">
            <span className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-medium text-white/90 ring-1 ring-inset ring-white/20">
              <BadgeCheck className="h-4 w-4" aria-hidden="true" />
              {tl('heroBadge')}
            </span>
            <h2 className="mt-5 text-3xl font-bold leading-tight tracking-tight text-white xl:text-4xl">
              {tl('heroTitle')}
            </h2>

            <ul className="mt-8 space-y-5">
              {valueProps.map(({ titleKey, bodyKey, Icon }) => (
                <li key={titleKey} className="flex gap-3.5">
                  <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white/10 text-white ring-1 ring-inset ring-white/20">
                    <Icon className="h-5 w-5" aria-hidden="true" />
                  </span>
                  <div>
                    <p className="font-semibold text-white">{tl(titleKey)}</p>
                    <p className="mt-0.5 text-sm text-white/70">{tl(bodyKey)}</p>
                  </div>
                </li>
              ))}
            </ul>
          </div>

          <ul className="grid grid-cols-2 gap-3">
            {trustPoints.map(({ key, Icon }) => (
              <li
                key={key}
                className="flex items-center gap-2 rounded-xl bg-white/10 px-3 py-2.5 text-sm text-white/90 ring-1 ring-inset ring-white/15"
              >
                <Icon className="h-4 w-4 shrink-0 text-money-300" aria-hidden="true" />
                <span className="font-medium">{tl(key)}</span>
              </li>
            ))}
          </ul>
        </div>
      </aside>

      {/* ---- Right: form area ---------------------------------------------- */}
      <div className="flex flex-1 flex-col">
        {/* Mobile header (brand + language); lg+ shows only the language switch. */}
        <header className="flex items-center justify-between px-4 py-4 lg:justify-end lg:px-8">
          <Link href="/" className="flex items-center gap-2 lg:hidden">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-600 text-white">
              <ShieldCheck className="h-5 w-5" aria-hidden="true" />
            </span>
            <span className="text-base font-semibold text-ink-900">{t('appName')}</span>
          </Link>
          <LanguageSwitcher />
        </header>

        <main className="flex flex-1 items-center justify-center px-4 py-6 sm:py-10">
          <div className="w-full max-w-md">
            <div className="animate-fade-in rounded-2xl border border-ink-200 bg-white p-6 shadow-card sm:p-8">
              {children}
            </div>

            {/* Compact trust footer — keeps assurances visible on mobile. */}
            <ul className="mt-5 flex flex-wrap items-center justify-center gap-x-4 gap-y-2 text-xs text-ink-500 lg:hidden">
              <li className="flex items-center gap-1.5">
                <Lock className="h-3.5 w-3.5 text-money-600" aria-hidden="true" />
                {tl('trustEncryption')}
              </li>
              <li className="flex items-center gap-1.5">
                <MapPin className="h-3.5 w-3.5 text-money-600" aria-hidden="true" />
                {tl('trustDataIndia')}
              </li>
            </ul>
          </div>
        </main>
      </div>
    </div>
  );
}
