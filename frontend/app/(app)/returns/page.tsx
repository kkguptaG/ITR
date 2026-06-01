'use client';

// ---------------------------------------------------------------------------
// /returns — the user's returns, paged + filterable by status.
//   • Table/cards: AY · ITR type · status · created · Continue/View actions
//   • "New return" opens the NewReturnDialog (AY + auto/explicit ITR) then
//     routes into the wizard at /returns/{id}/file/personal.
// Data via TanStack Query: GET /returns?status=&page=&pageSize=.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Plus, FileText, ChevronLeft, ChevronRight } from 'lucide-react';
import {
  Button,
  Select,
  Spinner,
  Alert,
  EmptyState,
  Card,
} from '@/components/ui';
import type { ReturnStatus } from '@/lib/api-types';
import {
  listReturns,
  returnsKeys,
  NewReturnDialog,
  ReturnsTable,
} from '@/features/returns';

const PAGE_SIZE = 10;

// Status filter options (value '' = all). Labels resolve from messages.status.*.
const STATUS_VALUES: ReturnStatus[] = [
  'Draft',
  'InProgress',
  'ComputedReady',
  'PendingPayment',
  'Paid',
  'UnderCaReview',
  'ReadyToFile',
  'Filed',
  'Processed',
  'Failed',
];

export default function ReturnsPage() {
  const t = useTranslations('returns');
  const ts = useTranslations('status');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [page, setPage] = useState(1);

  const params = {
    page,
    pageSize: PAGE_SIZE,
    status: statusFilter || undefined,
  };

  const query = useQuery({
    queryKey: returnsKeys.list(params),
    queryFn: () => listReturns(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const hasFilter = statusFilter !== '';

  const statusOptions = [
    { value: '', label: t('filterAllStatuses') },
    ...STATUS_VALUES.map((s) => ({ value: s, label: ts(s) })),
  ];

  return (
    <div className="space-y-6">
      {/* Header + CTA */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-ink-900">{t('pageTitle')}</h1>
          <p className="mt-1 text-sm text-ink-500">{t('pageSubtitle')}</p>
        </div>
        <Button size="lg" onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" aria-hidden="true" />
          {t('startNewReturn')}
        </Button>
      </div>

      {/* Filter bar */}
      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="status-filter" className="text-sm font-medium text-ink-600">
          {t('filterStatus')}
        </label>
        <div className="w-full max-w-[16rem]">
          <Select
            id="status-filter"
            value={statusFilter}
            options={statusOptions}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
          />
        </div>
        {!query.isLoading && (
          <span className="text-sm text-ink-500">
            {t('resultCount', { count: total })}
          </span>
        )}
      </div>

      {/* Body */}
      {query.isLoading ? (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner size={28} label={t('loadingList')} />
        </div>
      ) : query.isError ? (
        <Alert variant="error">{t('listError')}</Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={FileText}
          title={hasFilter ? t('emptyFilteredTitle') : t('emptyTitle')}
          description={hasFilter ? t('emptyFilteredBody') : t('emptyBody')}
          action={
            hasFilter ? (
              <Button variant="outline" onClick={() => { setStatusFilter(''); setPage(1); }}>
                {t('clearFilter')}
              </Button>
            ) : (
              <Button onClick={() => setDialogOpen(true)}>
                <Plus className="h-4 w-4" aria-hidden="true" />
                {t('startCta')}
              </Button>
            )
          }
        />
      ) : (
        <>
          <ReturnsTable items={items} />

          {/* Pagination */}
          {totalPages > 1 && (
            <Card className="flex items-center justify-between p-3">
              <Button
                variant="ghost"
                size="sm"
                disabled={page <= 1 || query.isFetching}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                {t('prev')}
              </Button>
              <span className="text-sm text-ink-500">
                {t('pageOf', { page, total: totalPages })}
              </span>
              <Button
                variant="ghost"
                size="sm"
                disabled={page >= totalPages || query.isFetching}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                {t('next')}
                <ChevronRight className="h-4 w-4" aria-hidden="true" />
              </Button>
            </Card>
          )}
        </>
      )}

      <NewReturnDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </div>
  );
}
