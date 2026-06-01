'use client';

// /support/tickets/[ticketId] — the support ticket thread view (read messages,
// post replies, change status). Data via GET /tickets/{id}.

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowLeft } from 'lucide-react';
import { TicketThread } from '@/features/support';

export default function TicketThreadPage({ params }: { params: { ticketId: string } }) {
  const t = useTranslations('support');
  return (
    <div className="space-y-5">
      <Link
        href="/support?tab=tickets"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-ink-500 hover:text-ink-800"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        {t('backToTickets')}
      </Link>
      <TicketThread ticketId={params.ticketId} />
    </div>
  );
}
