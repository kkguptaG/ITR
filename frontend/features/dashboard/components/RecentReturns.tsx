'use client';

// RecentReturns — compact recent-returns list for the dashboard. Shows up to a
// handful of the newest returns with status + the Continue/View action, plus a
// "View all" link to /returns. Empty state nudges a first filing.

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { FileText, ChevronRight, Plus } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  EmptyState,
  Button,
} from '@/components/ui';
import { formatAssessmentYear, formatDate } from '@/lib/format';
import type { ReturnSummaryDto } from '@/features/returns/types';
import { formatItrType } from '@/features/returns/helpers';
import { ReturnStatusBadge } from '@/features/returns/components/ReturnStatusBadge';
import { ReturnRowActions } from '@/features/returns/components/ReturnRowActions';

export interface RecentReturnsProps {
  items: ReturnSummaryDto[];
  total: number;
  onNewReturn: () => void;
}

export function RecentReturns({ items, total, onNewReturn }: RecentReturnsProps) {
  const t = useTranslations('returns');
  const tc = useTranslations('common');

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <CardTitle>{t('recentReturns')}</CardTitle>
        {total > items.length && (
          <Link
            href="/returns"
            className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700"
          >
            {tc('viewAll')}
            <ChevronRight className="h-4 w-4" aria-hidden="true" />
          </Link>
        )}
      </CardHeader>
      <CardContent>
        {items.length === 0 ? (
          <EmptyState
            icon={FileText}
            title={t('emptyTitle')}
            description={t('emptyBody')}
            action={
              <Button onClick={onNewReturn}>
                <Plus className="h-4 w-4" aria-hidden="true" />
                {t('startCta')}
              </Button>
            }
          />
        ) : (
          <ul className="divide-y divide-ink-100">
            {items.map((item) => (
              <li key={item.id} className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="font-medium text-ink-900">
                      {formatAssessmentYear(item.assessmentYear)}
                    </p>
                    <ReturnStatusBadge status={item.status} />
                  </div>
                  <p className="mt-0.5 truncate text-sm text-ink-500">
                    {item.itrType ? formatItrType(item.itrType) : t('itrPending')}
                    <span className="px-1.5 text-ink-300">·</span>
                    {formatDate(item.createdAt)}
                  </p>
                </div>
                <ReturnRowActions item={item} />
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
