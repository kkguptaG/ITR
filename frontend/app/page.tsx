import Link from 'next/link';
import { useTranslations } from 'next-intl';
import {
  ShieldCheck,
  Lock,
  MapPin,
  BadgeCheck,
  ArrowRight,
  Wand2,
  Scale,
  UserCheck,
  Upload,
  ListChecks,
  Coins,
} from 'lucide-react';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';

// NOTE: the app reads the locale from a cookie in i18n.ts, which opts every
// route into dynamic rendering — so we don't force-static here. For production
// SEO this page would move under a static [locale] segment (see docs 08 §8.5.5).

const trustIcons = {
  trustEncryption: Lock,
  trustDataIndia: MapPin,
  trustDpdp: ShieldCheck,
  trustCaVerified: BadgeCheck,
} as const;

const featureIcons = [Wand2, Scale, ShieldCheck, UserCheck] as const;
const stepIcons = [Upload, ListChecks, Scale, Coins] as const;

export default function LandingPage() {
  const t = useTranslations('landing');
  const tc = useTranslations('common');

  const trustKeys = ['trustEncryption', 'trustDataIndia', 'trustDpdp', 'trustCaVerified'] as const;
  const features = [
    { titleKey: 'featureWizardTitle', bodyKey: 'featureWizardBody' },
    { titleKey: 'featureRegimeTitle', bodyKey: 'featureRegimeBody' },
    { titleKey: 'featureSecureTitle', bodyKey: 'featureSecureBody' },
    { titleKey: 'featureExpertTitle', bodyKey: 'featureExpertBody' },
  ] as const;
  const steps = ['step1', 'step2', 'step3', 'step4'] as const;

  return (
    <div className="min-h-screen bg-ink-50">
      {/* Header */}
      <header className="sticky top-0 z-20 border-b border-ink-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 lg:px-8">
          <div className="flex items-center gap-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-600 text-white">
              <ShieldCheck className="h-5 w-5" aria-hidden="true" />
            </span>
            <span className="text-base font-semibold text-ink-900">{tc('appName')}</span>
          </div>
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <Link
              href="/login"
              className="hidden rounded-xl px-4 py-2 text-sm font-medium text-ink-700 hover:bg-ink-100 sm:block"
            >
              {t('ctaLogin')}
            </Link>
            <Link
              href="/register"
              className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-sm font-medium text-white shadow-soft transition-colors hover:bg-brand-700"
            >
              {t('ctaStart')}
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </Link>
          </div>
        </div>
      </header>

      <main>
        {/* Hero */}
        <section className="bg-hero-gradient">
          <div className="bg-grid">
            <div className="mx-auto max-w-7xl px-4 py-20 lg:px-8 lg:py-28">
              <div className="mx-auto max-w-3xl text-center">
                <span className="inline-flex items-center gap-2 rounded-full border border-brand-200 bg-white px-3 py-1 text-xs font-medium text-brand-700 shadow-sm">
                  <BadgeCheck className="h-4 w-4" aria-hidden="true" />
                  {t('heroBadge')}
                </span>
                <h1 className="mt-6 text-4xl font-bold tracking-tight text-ink-900 sm:text-5xl lg:text-6xl">
                  {t('heroTitle')}
                </h1>
                <p className="mx-auto mt-5 max-w-2xl text-lg text-ink-600">{t('heroSubtitle')}</p>
                <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
                  <Link
                    href="/register"
                    className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-6 py-3 text-base font-semibold text-white shadow-soft transition-colors hover:bg-brand-700 sm:w-auto"
                  >
                    {t('ctaStart')}
                    <ArrowRight className="h-5 w-5" aria-hidden="true" />
                  </Link>
                  <Link
                    href="/login"
                    className="inline-flex w-full items-center justify-center rounded-xl border border-ink-300 bg-white px-6 py-3 text-base font-semibold text-ink-800 transition-colors hover:bg-ink-50 sm:w-auto"
                  >
                    {t('ctaLogin')}
                  </Link>
                </div>

                {/* Trust strip */}
                <ul className="mx-auto mt-12 grid max-w-2xl grid-cols-2 gap-3 sm:grid-cols-4">
                  {trustKeys.map((key) => {
                    const Icon = trustIcons[key];
                    return (
                      <li
                        key={key}
                        className="flex flex-col items-center gap-1.5 rounded-xl border border-ink-200 bg-white/70 px-3 py-3 text-center"
                      >
                        <Icon className="h-5 w-5 text-money-600" aria-hidden="true" />
                        <span className="text-xs font-medium text-ink-700">{t(key)}</span>
                      </li>
                    );
                  })}
                </ul>
              </div>
            </div>
          </div>
        </section>

        {/* Features */}
        <section className="mx-auto max-w-7xl px-4 py-16 lg:px-8 lg:py-24">
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {features.map((f, i) => {
              const Icon = featureIcons[i];
              return (
                <div
                  key={f.titleKey}
                  className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card"
                >
                  <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-50 text-brand-600">
                    <Icon className="h-6 w-6" aria-hidden="true" />
                  </span>
                  <h3 className="mt-4 text-base font-semibold text-ink-900">{t(f.titleKey)}</h3>
                  <p className="mt-1.5 text-sm text-ink-600">{t(f.bodyKey)}</p>
                </div>
              );
            })}
          </div>
        </section>

        {/* Steps */}
        <section className="border-t border-ink-200 bg-white">
          <div className="mx-auto max-w-7xl px-4 py-16 lg:px-8 lg:py-24">
            <h2 className="text-center text-2xl font-bold tracking-tight text-ink-900 sm:text-3xl">
              {t('stepsTitle')}
            </h2>
            <ol className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
              {steps.map((step, i) => {
                const Icon = stepIcons[i];
                return (
                  <li key={step} className="relative flex flex-col items-center text-center">
                    <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-money-50 text-money-600">
                      <Icon className="h-7 w-7" aria-hidden="true" />
                    </span>
                    <span className="mt-4 text-xs font-semibold uppercase tracking-wider text-brand-600">
                      {i + 1}
                    </span>
                    <p className="mt-1 text-sm font-medium text-ink-800">{t(step)}</p>
                  </li>
                );
              })}
            </ol>
            <div className="mt-12 text-center">
              <Link
                href="/register"
                className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-6 py-3 text-base font-semibold text-white shadow-soft transition-colors hover:bg-brand-700"
              >
                {t('ctaStart')}
                <ArrowRight className="h-5 w-5" aria-hidden="true" />
              </Link>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-ink-200 bg-ink-50">
        <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-3 px-4 py-8 text-sm text-ink-500 sm:flex-row lg:px-8">
          <div className="flex items-center gap-2">
            <ShieldCheck className="h-4 w-4 text-brand-600" aria-hidden="true" />
            <span className="font-medium text-ink-700">{tc('appName')}</span>
          </div>
          <p>
            © {new Date().getFullYear()} {tc('appName')}. {t('footerRights')}
          </p>
        </div>
      </footer>
    </div>
  );
}
