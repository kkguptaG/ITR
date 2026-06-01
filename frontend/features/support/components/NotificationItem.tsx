'use client';

// NotificationItem — one row in the notification dropdown/list. Unread rows get
// a brand dot + tinted background; the channel is shown as a small chip.

import { useTranslations } from 'next-intl';
import { Badge } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatRelative } from '@/lib/format';
import type { NotificationDto } from '../types';
import { channelTone } from '../helpers';

export function NotificationItem({
  n,
  onMarkRead,
  compact = false,
}: {
  n: NotificationDto;
  onMarkRead?: (id: string) => void;
  compact?: boolean;
}) {
  const t = useTranslations('support');
  const title = n.title || n.template;

  return (
    <div
      className={cn(
        'flex gap-3 px-3 py-3',
        !n.isRead && 'bg-brand-50/50',
        compact ? 'rounded-lg' : 'rounded-xl border border-ink-100',
      )}
    >
      <span
        className={cn(
          'mt-1.5 h-2 w-2 shrink-0 rounded-full',
          n.isRead ? 'bg-transparent' : 'bg-brand-500',
        )}
        aria-hidden="true"
      />
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <p className={cn('truncate text-sm', n.isRead ? 'text-ink-700' : 'font-medium text-ink-900')}>
            {title}
          </p>
          <time className="shrink-0 text-xs text-ink-400">{formatRelative(n.createdAt)}</time>
        </div>
        {n.body && <p className="mt-0.5 line-clamp-2 text-sm text-ink-500">{n.body}</p>}
        <div className="mt-1.5 flex items-center gap-2">
          <Badge tone={channelTone[n.channel] ?? 'neutral'}>{n.channel}</Badge>
          {!n.isRead && onMarkRead && (
            <button
              type="button"
              onClick={() => onMarkRead(n.id)}
              className="text-xs font-medium text-brand-600 hover:text-brand-700"
            >
              {t('markRead')}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
