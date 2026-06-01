'use client';

// TicketsPanel — the support tickets list for the /support page. Status filter,
// a "New ticket" CTA (opens NewTicketDialog), a table of the caller's tickets,
// and pagination. Rows link to the thread view at /support/tickets/{id}.

import { useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Plus, Ticket as TicketIcon, ChevronLeft, ChevronRight, ArrowRight } from 'lucide-react';
import {
  Table,
  THead,
  TBody,
  TR,
  TH,
  TD,
  Badge,
  Button,
  Select,
  Spinner,
  Alert,
  EmptyState,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatRelative } from '@/lib/format';
import { listTickets, supportKeys } from '../api';
import { TICKET_STATUSES, ticketPriorityTone, ticketStatusTone } from '../helpers';
import { NewTicketDialog } from './NewTicketDialog';

const PAGE_SIZE = 15;

export function TicketsPanel() {
  const t = useTranslations('support');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);

  const params = { page, pageSize: PAGE_SIZE, status: statusFilter || undefined };
  const query = useQuery({
    queryKey: supportKeys.ticketList(params),
    queryFn: () => listTickets(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const statusOptions = [
    { value: '', label: t('filterAllStatuses') },
    ...TICKET_STATUSES.map((s) => ({ value: s, label: t(`ticketStatus.${s}` as 'ticketStatus.Open') })),
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="w-full sm:w-56">
          <Select
            aria-label={t('filterStatus')}
            options={statusOptions}
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
          />
        </div>
        <Button onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" aria-hidden="true" />
          {t('newTicket')}
        </Button>
      </div>

      {query.isLoading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : query.isError ? (
        <Alert variant="error" title={t('ticketsLoadError')}>
          {(query.error as ApiError).message}
        </Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={TicketIcon}
          title={t('noTicketsTitle')}
          description={t('noTicketsBody')}
          action={
            <Button onClick={() => setDialogOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden="true" />
              {t('newTicket')}
            </Button>
          }
        />
      ) : (
        <>
          {/* Desktop table */}
          <div className="hidden sm:block">
            <Table>
              <THead>
                <TR className="hover:bg-transparent">
                  <TH>{t('colSubject')}</TH>
                  <TH>{t('colCategory')}</TH>
                  <TH>{t('colPriority')}</TH>
                  <TH>{t('colStatus')}</TH>
                  <TH>{t('colUpdated')}</TH>
                  <TH className="text-right">{t('colAction')}</TH>
                </TR>
              </THead>
              <TBody>
                {items.map((item) => (
                  <TR key={item.id}>
                    <TD className="font-medium text-ink-900">{item.subject}</TD>
                    <TD className="text-ink-600">{item.category ?? '—'}</TD>
                    <TD>
                      <Badge tone={ticketPriorityTone[item.priority] ?? 'neutral'}>
                        {item.priority}
                      </Badge>
                    </TD>
                    <TD>
                      <Badge tone={ticketStatusTone[item.status] ?? 'neutral'}>
                        {item.status}
                      </Badge>
                    </TD>
                    <TD className="whitespace-nowrap text-ink-500">
                      {formatRelative(item.updatedAt)}
                    </TD>
                    <TD className="text-right">
                      <Link
                        href={`/support/tickets/${item.id}`}
                        className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700"
                      >
                        {t('open')}
                        <ArrowRight className="h-4 w-4" aria-hidden="true" />
                      </Link>
                    </TD>
                  </TR>
                ))}
              </TBody>
            </Table>
          </div>

          {/* Mobile cards */}
          <ul className="space-y-3 sm:hidden">
            {items.map((item) => (
              <li key={item.id}>
                <Link
                  href={`/support/tickets/${item.id}`}
                  className="block rounded-2xl border border-ink-200 bg-white p-4 shadow-card"
                >
                  <div className="flex items-start justify-between gap-3">
                    <p className="font-semibold text-ink-900">{item.subject}</p>
                    <Badge tone={ticketStatusTone[item.status] ?? 'neutral'}>{item.status}</Badge>
                  </div>
                  <div className="mt-2 flex items-center justify-between">
                    <Badge tone={ticketPriorityTone[item.priority] ?? 'neutral'}>
                      {item.priority}
                    </Badge>
                    <span className="text-xs text-ink-500">{formatRelative(item.updatedAt)}</span>
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          {totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-ink-500">{t('pageOf', { page, total: totalPages })}</p>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1 || query.isFetching}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                  {t('prev')}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= totalPages || query.isFetching}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  {t('next')}
                  <ChevronRight className="h-4 w-4" aria-hidden="true" />
                </Button>
              </div>
            </div>
          )}
        </>
      )}

      <NewTicketDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </div>
  );
}
