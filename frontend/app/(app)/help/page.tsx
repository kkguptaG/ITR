'use client';

// ---------------------------------------------------------------------------
// /help — Help Centre. Grouped FAQs (translatable) + contact CTAs (raise a
// ticket, view notices). Self-contained; FAQ copy lives under messages.help.*.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { LifeBuoy, MessageSquarePlus, ShieldCheck } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
} from '@/components/ui';
import { faqGroups } from '@/features/help/faqs';
import { FaqItem } from '@/features/help/components/FaqItem';
import { Form10ECard } from '@/features/help/components/Form10ECard';

export default function HelpPage() {
  const t = useTranslations('help');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-ink-900">{t('pageTitle')}</h1>
        <p className="mt-1 text-sm text-ink-500">{t('pageSubtitle')}</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* FAQs */}
        <div className="space-y-6 lg:col-span-2">
          {faqGroups.map((group) => (
            <Card key={group.titleKey}>
              <CardHeader>
                <CardTitle>{t(`group.${group.titleKey}`)}</CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="divide-y divide-ink-100">
                  {group.ids.map((id) => (
                    <FaqItem
                      key={id}
                      question={t(`faq.${id}.q`)}
                      answer={t(`faq.${id}.a`)}
                    />
                  ))}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>

        {/* Contact rail */}
        <div className="space-y-6">
          <Form10ECard />

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <LifeBuoy className="h-5 w-5 text-brand-600" aria-hidden="true" />
                {t('contactTitle')}
              </CardTitle>
              <CardDescription>{t('contactBody')}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <Link
                href="/tickets"
                className="inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl border border-ink-300 bg-white px-4 text-sm font-medium text-ink-800 transition-colors hover:bg-ink-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2"
              >
                <MessageSquarePlus className="h-4 w-4" aria-hidden="true" />
                {t('raiseTicket')}
              </Link>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="flex items-start gap-3 p-5">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-money-50 text-money-700">
                <ShieldCheck className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <p className="text-sm font-medium text-ink-900">{t('trustTitle')}</p>
                <p className="mt-0.5 text-sm text-ink-500">{t('trustBody')}</p>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
