'use client';

// NoticesPanel — the ITD notices vault for the /support page. Status filter, an
// "Add notice" CTA (opens NoticeUploadDialog), a table of the caller's notices
// with due-date urgency, and pagination. Rows link to /support/notices/{id}.

import { useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  Plus,
  Bell,
  Paperclip,
  ChevronLeft,
  ChevronRight,
  ArrowRight,
} from 'lucide-react';
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
import { formatDate, formatInr } from '@/lib/format';
import { listNotices, supportKeys } from '../api';
import { NOTICE_STATUSES, noticeStatusTone } from '../helpers';
import { NoticeUploadDialog } from './NoticeUploadDialog';

const PAGE_SIZE = 15;

/** Due-date chip: overdue (red), due soon ≤7d (amber), else neutral date. */
function DueChip({ dueDate }: { dueDate?: string | null }) {
  const t = useTranslations('support');
  if (!dueDate) return <span className="text-ink-400">—</span>;
  const due = new Date(dueDate).getTime();
  if (Number.isNaN(due)) return <span className="text-ink-400">—</span>;
  const days = Math.ceil((due - Date.now()) / (24 * 60 * 60 * 1000));
  if (days < 0) return <Badge tone="danger">{t('overdue')}</Badge>;
  if (days <= 7) return <Badge tone="warning">{t('dueInDays', { days })}</Badge>;
  return <span className="text-ink-600">{formatDate(dueDate)}</span>;
}

export function NoticesPanel() {
  const t = useTranslations('support');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);

  const params = { page, pageSize: PAGE_SIZE, status: statusFilter || undefined };
  const query = useQuery({
    queryKey: supportKeys.noticeList(params),
    queryFn: () => listNotices(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const statusOptions = [
    { value: '', label: t('filterAllStatuses') },
    ...NOTICE_STATUSES.map((s) => ({
      value: s,
      label: t(`noticeStatus.${s}` as 'noticeStatus.Open'),
    })),
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
          {t('addNotice')}
        </Button>
      </div>

      {query.isLoading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : query.isError ? (
        <Alert variant="error" title={t('noticesLoadError')}>
          {(query.error as ApiError).message}
        </Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={Bell}
          title={t('noNoticesTitle')}
          description={t('noNoticesBody')}
          action={
            <Button onClick={() => setDialogOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden="true" />
              {t('addNotice')}
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
                  <TH>{t('colNoticeType')}</TH>
                  <TH>{t('colReceived')}</TH>
                  <TH>{t('colDue')}</TH>
                  <TH className="text-right">{t('colDemand')}</TH>
                  <TH>{t('colStatus')}</TH>
                  <TH className="text-right">{t('colAction')}</TH>
                </TR>
              </THead>
              <TBody>
                {items.map((item) => (
                  <TR key={item.id}>
                    <TD className="font-medium text-ink-900">
                      <span className="flex items-center gap-2">
                        {item.noticeType}
                        {item.hasAttachment && (
                          <Paperclip className="h-3.5 w-3.5 text-ink-400" aria-hidden="true" />
                        )}
                      </span>
                      {item.section && (
                        <span className="text-xs text-ink-400">{item.section}</span>
                      )}
                    </TD>
                    <TD className="whitespace-nowrap text-ink-600">{formatDate(item.receivedAt)}</TD>
                    <TD className="whitespace-nowrap">
                      <DueChip dueDate={item.dueDate} />
                    </TD>
                    <TD className="text-right text-ink-700">
                      {item.demandAmount ? formatInr(item.demandAmount) : '—'}
                    </TD>
                    <TD>
                      <Badge tone={noticeStatusTone[item.status] ?? 'neutral'}>
                        {item.status}
                      </Badge>
                    </TD>
                    <TD className="text-right">
                      <Link
                        href={`/support/notices/${item.id}`}
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
                  href={`/support/notices/${item.id}`}
                  className="block rounded-2xl border border-ink-200 bg-white p-4 shadow-card"
                >
                  <div className="flex items-start justify-between gap-3">
                    <p className="font-semibold text-ink-900">{item.noticeType}</p>
                    <Badge tone={noticeStatusTone[item.status] ?? 'neutral'}>{item.status}</Badge>
                  </div>
                  <div className="mt-2 flex items-center justify-between text-sm">
                    <span className="text-ink-500">{formatDate(item.receivedAt)}</span>
                    <DueChip dueDate={item.dueDate} />
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

      <NoticeUploadDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </div>
  );
}
