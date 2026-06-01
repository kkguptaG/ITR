import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { Compass } from 'lucide-react';

/** 404 page. */
export default async function NotFound() {
  const t = await getTranslations('notFound');
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-ink-50 px-6 text-center">
      <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-brand-50 text-brand-600">
        <Compass className="h-7 w-7" aria-hidden="true" />
      </span>
      <p className="text-5xl font-bold tracking-tight text-ink-900">404</p>
      <h1 className="text-xl font-semibold text-ink-900">{t('title')}</h1>
      <p className="max-w-sm text-sm text-ink-500">{t('body')}</p>
      <Link
        href="/dashboard"
        className="mt-2 inline-flex items-center justify-center rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white shadow-soft transition-colors hover:bg-brand-700"
      >
        {t('cta')}
      </Link>
    </div>
  );
}
