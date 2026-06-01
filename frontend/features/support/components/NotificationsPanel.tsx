'use client';

// NotificationsPanel — the full notification inbox for the /support page.
// Lists notifications (paged), filter to unread-only, mark one / mark all read.

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BellOff, CheckCheck, ChevronLeft, ChevronRight } from 'lucide-react';
import { Card, CardContent, Spinner, Alert, EmptyState, Button } from '@/components/ui';
import { cn } from '@/lib/utils';
import { ApiError } from '@/lib/api';
import {
  listNotifications,
  markNotificationsRead,
  supportKeys,
} from '../api';
import { NotificationItem } from './NotificationItem';

const PAGE_SIZE = 15;

export function NotificationsPanel() {
  const t = useTranslations('support');
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);

  const params = { page, pageSize: PAGE_SIZE, unreadOnly };
  const query = useQuery({
    queryKey: supportKeys.notificationList(params),
    queryFn: () => listNotifications(params),
    placeholderData: keepPreviousData,
  });

  const markRead = useMutation({
    mutationFn: (ids: string[] | null) => markNotificationsRead({ ids }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: supportKeys.notifications }),
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="inline-flex rounded-xl bg-ink-100 p-1">
          <button
            type="button"
            onClick={() => {
              setUnreadOnly(false);
              setPage(1);
            }}
            className={cn(
              'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
              !unreadOnly ? 'bg-white text-ink-900 shadow-sm' : 'text-ink-500 hover:text-ink-800',
            )}
          >
            {t('filterAll')}
          </button>
          <button
            type="button"
            onClick={() => {
              setUnreadOnly(true);
              setPage(1);
            }}
            className={cn(
              'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
              unreadOnly ? 'bg-white text-ink-900 shadow-sm' : 'text-ink-500 hover:text-ink-800',
            )}
          >
            {t('filterUnread')}
          </button>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => markRead.mutate(null)}
          loading={markRead.isPending}
        >
          <CheckCheck className="h-4 w-4" aria-hidden="true" />
          {t('markAllRead')}
        </Button>
      </div>

      {query.isLoading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : query.isError ? (
        <Alert variant="error" title={t('notificationsLoadError')}>
          {(query.error as ApiError).message}
        </Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={BellOff}
          title={unreadOnly ? t('noUnreadTitle') : t('noNotificationsTitle')}
          description={t('noNotificationsBody')}
        />
      ) : (
        <>
          <Card>
            <CardContent className="space-y-2 p-3">
              {items.map((n) => (
                <NotificationItem key={n.id} n={n} onMarkRead={(id) => markRead.mutate([id])} />
              ))}
            </CardContent>
          </Card>

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
    </div>
  );
}
