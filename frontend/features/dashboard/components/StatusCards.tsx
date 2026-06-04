'use client';

// ---------------------------------------------------------------------------
// StatusCards — the four headline cards on the dashboard: filing-status gauge,
// estimated refund, due date, and filing mode. Pure/presentational; data is
// fetched in the dashboard page and passed in.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowDownToLine, ArrowUpFromLine, CalendarClock, ChevronRight, Sparkles } from 'lucide-react';
import { Card, ProgressRing } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, formatDate } from '@/lib/format';
import type { ReturnSummaryDto } from '@/features/returns/types';
import { returnHref } from '@/features/returns/helpers';
import type { DeadlineInfo } from '../deadlines';

/** Map a return's lifecycle status to a coarse completion % + a translation key for the gauge. */
export function filingProgress(status: ReturnSummaryDto['status']): { pct: number; key: string } {
  switch (status) {
    case 'Draft': return { pct: 15, key: 'Draft' };
    case 'InProgress': return { pct: 45, key: 'InProgress' };
    case 'ComputedReady': return { pct: 65, key: 'ComputedReady' };
    case 'PendingPayment': return { pct: 70, key: 'PendingPayment' };
    case 'Paid': return { pct: 80, key: 'Paid' };
    case 'UnderCaReview': return { pct: 82, key: 'UnderCaReview' };
    case 'ReadyToFile': return { pct: 85, key: 'ReadyToFile' };
    case 'Filed': return { pct: 92, key: 'Filed' };
    case 'Processed': return { pct: 100, key: 'Processed' };
    case 'Failed': return { pct: 45, key: 'Failed' };
    default: return { pct: 10, key: 'NotStarted' };
  }
}

function MiniCard({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Card className="flex flex-col gap-3 p-5">
      <p className="text-sm font-medium text-ink-500">{label}</p>
      {children}
    </Card>
  );
}

function CardLink({ href, children }: { href: string; children: React.ReactNode }) {
  return (
    <Link href={href} className="mt-auto inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700">
      {children}
      <ChevronRight className="h-4 w-4" aria-hidden="true" />
    </Link>
  );
}

export function StatusCards({
  latest,
  refundOrPayable,
  regime,
  deadline,
}: {
  latest: ReturnSummaryDto;
  refundOrPayable: number | null;
  regime: string | null;
  deadline: DeadlineInfo | null;
}) {
  const t = useTranslations('home');
  const progress = filingProgress(latest.status);
  const isRefund = (refundOrPayable ?? 0) >= 0;
  const amount = Math.abs(refundOrPayable ?? 0);
  const href = returnHref(latest);

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {/* Filing status gauge */}
      <Card className="flex items-center gap-4 p-5">
        <ProgressRing value={progress.pct} size={84} colorClass="text-brand-600">
          <span className="text-lg font-semibold tabular-nums text-ink-900">{progress.pct}%</span>
        </ProgressRing>
        <div className="flex min-w-0 flex-col gap-1">
          <p className="text-sm font-medium text-ink-500">{t('filingStatus')}</p>
          <p className="text-base font-semibold text-ink-900">{t(`progress.${progress.key}`)}</p>
          <CardLink href={href}>{progress.pct >= 92 ? t('viewReturn') : t('continueFiling')}</CardLink>
        </div>
      </Card>

      {/* Estimated refund / payable */}
      <MiniCard label={isRefund ? t('estimatedRefund') : t('taxPayable')}>
        <div className="flex items-center gap-2">
          {isRefund ? (
            <ArrowDownToLine className="h-5 w-5 text-money-600" aria-hidden="true" />
          ) : (
            <ArrowUpFromLine className="h-5 w-5 text-payable-600" aria-hidden="true" />
          )}
          <span className={cn('text-2xl font-semibold tabular-nums', isRefund ? 'text-money-700' : 'text-payable-700')}>
            {formatInr(amount)}
          </span>
        </div>
        <p className="text-xs text-ink-500">{regime ? t('underRegime', { regime }) : t('computedFromReturn')}</p>
        <CardLink href={href}>{t('viewDetails')}</CardLink>
      </MiniCard>

      {/* Due date */}
      <MiniCard label={t('filingDueDate')}>
        <div className="flex items-center gap-2">
          <CalendarClock className="h-5 w-5 text-brand-600" aria-hidden="true" />
          <span className="text-2xl font-semibold tabular-nums text-ink-900">
            {deadline ? formatDate(deadline.dueDate) : '—'}
          </span>
        </div>
        <p className={cn('text-xs', deadline?.isPastDue ? 'text-payable-700' : 'text-ink-500')}>
          {deadline
            ? deadline.isPastDue
              ? t('dueDatePassed')
              : t('daysToGo', { days: deadline.daysToDue })
            : t('nonAuditDueDate')}
        </p>
        <CardLink href={href}>{t('viewReturn')}</CardLink>
      </MiniCard>

      {/* Filing mode */}
      <MiniCard label={t('filingMode')}>
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-brand-600" aria-hidden="true" />
          <span className="text-base font-semibold text-ink-900">{t('selfFiling')}</span>
        </div>
        <p className="text-xs text-ink-500">{t('expertHelpHint')}</p>
        <CardLink href="/support">{t('exploreExpertHelp')}</CardLink>
      </MiniCard>
    </div>
  );
}
