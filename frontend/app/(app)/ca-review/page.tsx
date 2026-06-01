'use client';

// ---------------------------------------------------------------------------
// /ca-review — the CA work queue (role-gated to CA/CaFirmAdmin/Reviewer via the
// sidebar; the server enforces the real authority). Lists assigned returns plus
// the firm's unassigned UnderCaReview pool, newest/most-urgent first, paged.
// Data: GET /ca/queue?page=&pageSize=  → PagedResult<QueueItemDto>.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ClipboardCheck, ChevronLeft, ChevronRight } from 'lucide-react';
import { Spinner, Alert, EmptyState, Button, Badge } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { getQueue, caKeys, CaQueueTable } from '@/features/ca';

const PAGE_SIZE = 15;

export default function CaReviewQueuePage() {
  const t = useTranslations('caReview');
  const [page, setPage] = useState(1);

  const params = { page, pageSize: PAGE_SIZE };
  const query = useQuery({
    queryKey: caKeys.queue(params),
    queryFn: () => getQueue(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-semibold text-ink-900">{t('queueTitle')}</h1>
          {total > 0 && <Badge tone="brand">{total}</Badge>}
        </div>
        <p className="text-sm text-ink-500">{t('queueSubtitle')}</p>
      </div>

      {query.isLoading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : query.isError ? (
        <Alert variant="error" title={t('queueLoadError')}>
          {(query.error as ApiError).message}
        </Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={ClipboardCheck}
          title={t('queueEmptyTitle')}
          description={t('queueEmptyBody')}
        />
      ) : (
        <>
          <CaQueueTable items={items} />

          {totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-ink-500">
                {t('pageOf', { page, total: totalPages })}
              </p>
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
    </div>
  );
}
