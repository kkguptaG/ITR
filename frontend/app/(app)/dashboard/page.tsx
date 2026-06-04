'use client';

// ---------------------------------------------------------------------------
// /dashboard — the signed-in home.
//   • Greeting + "Start new return" CTA
//   • KPI cards: returns in progress · refund expected · nearest deadline
//   • Recent returns list (+ View all)
//   • StatusTimeline for the latest return + RefundTrackerCard
//   • DeadlinesCard for the active assessment year
// Data via TanStack Query: GET /returns (list), GET /returns/{id} (latest
// detail → computation), GET /tax/slabs (active AY for deadlines + dialog).
// ---------------------------------------------------------------------------

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { FileClock, Wallet, CalendarClock, Plus, FileText } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  Button,
  Spinner,
  Alert,
} from '@/components/ui';
import { apiGet } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { formatInr } from '@/lib/format';
import {
  listReturns,
  getActiveAssessmentYear,
  returnsKeys,
  NewReturnDialog,
} from '@/features/returns';
import type { ReturnDetailDto } from '@/features/returns/types';
import { isInProgress, refundOrPayable } from '@/features/returns/helpers';
import { KpiCard } from '@/features/dashboard/components/KpiCard';
import { RecentReturns } from '@/features/dashboard/components/RecentReturns';
import { RefundTrackerCard } from '@/features/dashboard/components/RefundTrackerCard';
import { EVerifyReminder } from '@/features/dashboard/components/EVerifyReminder';
import { StatusTimeline } from '@/features/dashboard/components/StatusTimeline';
import { DeadlinesCard } from '@/features/dashboard/components/DeadlinesCard';
import { deadlineFor } from '@/features/dashboard/deadlines';

const RECENT_PAGE_SIZE = 5;

export default function DashboardPage() {
  const t = useTranslations('dashboard');
  const tr = useTranslations('returns');
  const { user } = useAuth();
  const [dialogOpen, setDialogOpen] = useState(false);

  // Recent returns (newest first; the list endpoint already orders by createdAt desc).
  const listQuery = useQuery({
    queryKey: returnsKeys.list({ page: 1, pageSize: RECENT_PAGE_SIZE }),
    queryFn: () => listReturns({ page: 1, pageSize: RECENT_PAGE_SIZE }),
  });

  const items = listQuery.data?.items ?? [];
  const total = listQuery.data?.total ?? 0;
  const latest = items[0];

  // Active AY for the deadline KPI + card (independent of whether the user has returns).
  const ayQuery = useQuery({
    queryKey: returnsKeys.activeAy,
    queryFn: getActiveAssessmentYear,
    staleTime: 60 * 60_000,
  });
  const activeAy = ayQuery.data?.assessmentYear;

  // Latest return detail → its computation drives the refund tracker + KPI.
  const latestDetailQuery = useQuery({
    queryKey: latest ? returnsKeys.detail(latest.id) : ['returns', 'detail', 'none'],
    queryFn: () => apiGet<ReturnDetailDto>(`/returns/${latest!.id}`),
    enabled: !!latest,
  });
  const latestComputation = latestDetailQuery.data?.latestComputation ?? null;

  // KPI values.
  const inProgressCount = useMemo(
    () => items.filter((r) => isInProgress(r.status)).length,
    [items],
  );

  const refundAmount = refundOrPayable(latestComputation);
  const refundValue =
    latestComputation && refundAmount >= 0 ? formatInr(refundAmount) : formatInr(0);

  const deadlineInfo = activeAy ? deadlineFor(activeAy) : null;
  const deadlineValue = deadlineInfo
    ? deadlineInfo.isPastDue
      ? tr('deadlinePassed')
      : tr('deadlineDaysShort', { days: deadlineInfo.daysToDue })
    : '—';

  const greeting = user?.fullName
    ? t('welcome', { name: user.fullName.split(' ')[0] })
    : t('welcomeGeneric');

  if (listQuery.isLoading) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner size={28} label={tr('loadingDashboard')} />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Greeting + CTA */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-ink-900">{greeting}</h1>
          <p className="mt-1 text-sm text-ink-500">{tr('dashboardSubtitle')}</p>
        </div>
        <Button size="lg" onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" aria-hidden="true" />
          {tr('startNewReturn')}
        </Button>
      </div>

      {listQuery.isError && <Alert variant="error">{tr('listError')}</Alert>}

      {/* Action needed: e-verify a filed return before its 30-day window lapses. */}
      <EVerifyReminder items={items} />

      {/* KPI cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <KpiCard
          icon={FileClock}
          tone="brand"
          label={tr('kpiInProgress')}
          value={String(inProgressCount)}
          sub={tr('kpiInProgressSub', { total })}
        />
        <KpiCard
          icon={Wallet}
          tone="money"
          label={tr('kpiRefundExpected')}
          value={refundValue}
          sub={latestComputation ? tr('kpiRefundSub') : tr('kpiRefundNone')}
        />
        <KpiCard
          icon={CalendarClock}
          tone={deadlineInfo?.isPastDue ? 'payable' : 'neutral'}
          label={tr('kpiDeadline')}
          value={deadlineValue}
          sub={activeAy ? tr('kpiDeadlineSub') : undefined}
        />
      </div>

      {/* Main grid: recent returns (wide) + latest status/refund (rail) */}
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <RecentReturns
            items={items}
            total={total}
            onNewReturn={() => setDialogOpen(true)}
          />
        </div>

        <div className="space-y-6">
          {latest ? (
            <>
              <RefundTrackerCard latest={latest} computation={latestComputation} />
              <Card>
                <CardHeader>
                  <CardTitle>{tr('latestActivity')}</CardTitle>
                </CardHeader>
                <CardContent>
                  <StatusTimeline
                    status={latest.status}
                    eVerified={!!latestDetailQuery.data?.eVerifiedAt}
                  />
                </CardContent>
              </Card>
            </>
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>{tr('getStarted')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex items-start gap-3 rounded-xl bg-brand-50 p-4">
                  <FileText className="mt-0.5 h-5 w-5 shrink-0 text-brand-600" aria-hidden="true" />
                  <p className="text-sm text-brand-900">{tr('getStartedBody')}</p>
                </div>
                <Button fullWidth onClick={() => setDialogOpen(true)}>
                  <Plus className="h-4 w-4" aria-hidden="true" />
                  {tr('startCta')}
                </Button>
              </CardContent>
            </Card>
          )}

          {activeAy && <DeadlinesCard assessmentYear={activeAy} />}
        </div>
      </div>

      <NewReturnDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </div>
  );
}
