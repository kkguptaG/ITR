'use client';

// ---------------------------------------------------------------------------
// StatusCards — the four headline cards on the dashboard: filing-status gauge,
// estimated refund, due date, and filing mode. Pure/presentational; data is
// fetched in the dashboard page and passed in.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { ArrowDownToLine, ArrowUpFromLine, CalendarClock, ChevronRight, Sparkles } from 'lucide-react';
import { Card, ProgressRing } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr, formatDate } from '@/lib/format';
import type { ReturnSummaryDto } from '@/features/returns/types';
import { returnHref } from '@/features/returns/helpers';
import type { DeadlineInfo } from '../deadlines';

/** Map a return's lifecycle status to a coarse completion % + label for the gauge. */
export function filingProgress(status: ReturnSummaryDto['status']): { pct: number; label: string } {
  switch (status) {
    case 'Draft': return { pct: 15, label: 'Draft' };
    case 'InProgress': return { pct: 45, label: 'In progress' };
    case 'ComputedReady': return { pct: 65, label: 'Computed' };
    case 'PendingPayment': return { pct: 70, label: 'Payment due' };
    case 'Paid': return { pct: 80, label: 'Paid' };
    case 'UnderCaReview': return { pct: 82, label: 'Under CA review' };
    case 'ReadyToFile': return { pct: 85, label: 'Ready to file' };
    case 'Filed': return { pct: 92, label: 'Filed' };
    case 'Processed': return { pct: 100, label: 'Processed' };
    case 'Failed': return { pct: 45, label: 'Action needed' };
    default: return { pct: 10, label: 'Not started' };
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
          <p className="text-sm font-medium text-ink-500">Filing status</p>
          <p className="text-base font-semibold text-ink-900">{progress.label}</p>
          <CardLink href={href}>{progress.pct >= 92 ? 'View return' : 'Continue filing'}</CardLink>
        </div>
      </Card>

      {/* Estimated refund / payable */}
      <MiniCard label={isRefund ? 'Estimated refund' : 'Tax payable'}>
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
        <p className="text-xs text-ink-500">{regime ? `Under ${regime} regime` : 'Computed from your return'}</p>
        <CardLink href={href}>View details</CardLink>
      </MiniCard>

      {/* Due date */}
      <MiniCard label="Filing due date">
        <div className="flex items-center gap-2">
          <CalendarClock className="h-5 w-5 text-brand-600" aria-hidden="true" />
          <span className="text-2xl font-semibold tabular-nums text-ink-900">
            {deadline ? formatDate(deadline.dueDate) : '—'}
          </span>
        </div>
        <p className={cn('text-xs', deadline?.isPastDue ? 'text-payable-700' : 'text-ink-500')}>
          {deadline
            ? deadline.isPastDue
              ? 'Due date has passed'
              : `${deadline.daysToDue} days to go`
            : 'Non-audit ITR due date'}
        </p>
        <CardLink href={href}>View return</CardLink>
      </MiniCard>

      {/* Filing mode */}
      <MiniCard label="Filing mode">
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-brand-600" aria-hidden="true" />
          <span className="text-base font-semibold text-ink-900">Self filing</span>
        </div>
        <p className="text-xs text-ink-500">Get a CA to review &amp; file for you.</p>
        <CardLink href="/support">Explore expert help</CardLink>
      </MiniCard>
    </div>
  );
}
