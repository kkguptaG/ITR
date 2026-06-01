'use client';

// /support/notices/[noticeId] — a single ITD notice: details, status, and the
// recorded responses (with the ability to add one). Data via GET /notices/{id}.

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowLeft } from 'lucide-react';
import { NoticeDetail } from '@/features/support';

export default function NoticeDetailPage({ params }: { params: { noticeId: string } }) {
  const t = useTranslations('support');
  return (
    <div className="space-y-5">
      <Link
        href="/support?tab=notices"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-ink-500 hover:text-ink-800"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        {t('backToNotices')}
      </Link>
      <NoticeDetail noticeId={params.noticeId} />
    </div>
  );
}
