'use client';

// NotificationsBell — a self-contained bell + dropdown driven by /notifications.
//   • polls the unread count (GET /notifications/unread-count) for the badge
//   • opens a dropdown listing the latest notifications
//   • mark-one-read / mark-all-read (POST /notifications:mark-read)
//   • "View all" links to /support?tab=notifications
// Drop it into any header/toolbar; it owns its own data + outside-click close.

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bell, CheckCheck } from 'lucide-react';
import { Spinner } from '@/components/ui';
import {
  getUnreadCount,
  listNotifications,
  markNotificationsRead,
  supportKeys,
} from '../api';
import { NotificationItem } from './NotificationItem';

const DROPDOWN_PARAMS = { page: 1, pageSize: 8 };

export function NotificationsBell() {
  const t = useTranslations('support');
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  // Unread badge — kept fresh with a light poll so the count updates over time.
  const unreadQuery = useQuery({
    queryKey: supportKeys.unreadCount,
    queryFn: getUnreadCount,
    refetchInterval: 60_000,
  });

  // Dropdown list — only fetched while the dropdown is open.
  const listQuery = useQuery({
    queryKey: supportKeys.notificationList(DROPDOWN_PARAMS),
    queryFn: () => listNotifications(DROPDOWN_PARAMS),
    enabled: open,
  });

  const markRead = useMutation({
    mutationFn: (ids: string[] | null) => markNotificationsRead({ ids }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: supportKeys.notifications });
    },
  });

  // Close on outside click / Escape (mirrors the Topbar account menu).
  useEffect(() => {
    if (!open) return;
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const unread = unreadQuery.data?.unread ?? 0;
  const items = listQuery.data?.items ?? [];

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('notificationsAria', { count: unread })}
        className="relative rounded-lg p-2 text-ink-600 hover:bg-ink-100"
      >
        <Bell className="h-5 w-5" aria-hidden="true" />
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-semibold text-white">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 z-40 mt-2 w-80 origin-top-right rounded-xl border border-ink-200 bg-white shadow-card animate-fade-in sm:w-96"
        >
          <div className="flex items-center justify-between border-b border-ink-100 px-4 py-3">
            <p className="text-sm font-semibold text-ink-900">{t('notifications')}</p>
            {unread > 0 && (
              <button
                type="button"
                onClick={() => markRead.mutate(null)}
                disabled={markRead.isPending}
                className="inline-flex items-center gap-1 text-xs font-medium text-brand-600 hover:text-brand-700 disabled:opacity-50"
              >
                <CheckCheck className="h-3.5 w-3.5" aria-hidden="true" />
                {t('markAllRead')}
              </button>
            )}
          </div>

          <div className="max-h-96 overflow-y-auto p-1.5">
            {listQuery.isLoading ? (
              <div className="flex justify-center py-8">
                <Spinner />
              </div>
            ) : items.length === 0 ? (
              <p className="px-3 py-8 text-center text-sm text-ink-500">{t('noNotifications')}</p>
            ) : (
              items.map((n) => (
                <NotificationItem
                  key={n.id}
                  n={n}
                  compact
                  onMarkRead={(id) => markRead.mutate([id])}
                />
              ))
            )}
          </div>

          <div className="border-t border-ink-100 px-4 py-2.5 text-center">
            <Link
              href="/support?tab=notifications"
              onClick={() => setOpen(false)}
              className="text-sm font-medium text-brand-600 hover:text-brand-700"
            >
              {t('viewAllNotifications')}
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
