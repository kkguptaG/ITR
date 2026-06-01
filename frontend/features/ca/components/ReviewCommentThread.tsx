'use client';

// ReviewCommentThread — the chronological CA decision history on an assignment.
// Each entry shows the outcome (Approved / ChangesRequested), the reviewer, the
// note, and a relative timestamp.

import { useTranslations } from 'next-intl';
import { CheckCircle2, RotateCcw, MessageSquare } from 'lucide-react';
import { Badge, EmptyState } from '@/components/ui';
import { formatDateTime } from '@/lib/format';
import type { ReviewCommentDto } from '../types';

export function ReviewCommentThread({ comments }: { comments: ReviewCommentDto[] }) {
  const t = useTranslations('caReview');

  if (comments.length === 0) {
    return (
      <EmptyState
        icon={MessageSquare}
        title={t('noCommentsTitle')}
        description={t('noCommentsBody')}
      />
    );
  }

  return (
    <ol className="space-y-4">
      {comments.map((c) => {
        const approved = c.outcome === 'Approved';
        const Icon = approved ? CheckCircle2 : RotateCcw;
        return (
          <li key={c.id} className="flex gap-3">
            <span
              className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${
                approved ? 'bg-money-50 text-money-600' : 'bg-payable-50 text-payable-600'
              }`}
            >
              <Icon className="h-4 w-4" aria-hidden="true" />
            </span>
            <div className="min-w-0 flex-1 rounded-xl border border-ink-200 bg-white p-3.5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-ink-900">
                    {c.caName || t('reviewer')}
                  </span>
                  <Badge tone={approved ? 'success' : 'warning'}>
                    {approved ? t('outcomeApproved') : t('outcomeChanges')}
                  </Badge>
                </div>
                <time className="text-xs text-ink-400">{formatDateTime(c.createdAt)}</time>
              </div>
              {c.comments && (
                <p className="mt-2 whitespace-pre-wrap text-sm text-ink-700">{c.comments}</p>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
