'use client';

// ---------------------------------------------------------------------------
// /admin — back-office overview (console home).
//   • KPI cards from GET /admin/analytics/overview
//   • Revenue trend sparkline from GET /admin/analytics/revenue?granularity=month
//   • Filing funnel bars from GET /admin/analytics/filings
// Analytics endpoints are gated to Admin/SuperAdmin (revenue is sensitive); Ops
// users see the operational quick-links instead of the revenue widgets.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Users,
  FileText,
  FileClock,
  ClipboardCheck,
  IndianRupee,
  FolderCheck,
  UserPlus,
  ArrowRight,
  TrendingUp,
} from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Spinner,
  Alert,
} from '@/components/ui';
import { useAuth } from '@/lib/auth';
import { formatInr, formatNumber, toNumber } from '@/lib/format';
import {
  PageHeader,
  StatCard,
  Sparkline,
  BarChart,
  adminKeys,
  getOverview,
  getRevenue,
  getFilings,
} from '@/features/admin';

export default function AdminOverviewPage() {
  const { hasAnyRole } = useAuth();
  const canSeeAnalytics = hasAnyRole(['Admin', 'SuperAdmin']);

  const overviewQuery = useQuery({
    queryKey: adminKeys.overview(),
    queryFn: getOverview,
    enabled: canSeeAnalytics,
  });

  const revenueQuery = useQuery({
    queryKey: adminKeys.revenue('month'),
    queryFn: () => getRevenue('month'),
    enabled: canSeeAnalytics,
  });

  const filingsQuery = useQuery({
    queryKey: adminKeys.filings(),
    queryFn: getFilings,
    enabled: canSeeAnalytics,
  });

  const o = overviewQuery.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Console overview"
        subtitle="Operational health, filings and revenue at a glance."
      />

      {!canSeeAnalytics ? (
        <OpsQuickLinks />
      ) : overviewQuery.isLoading ? (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner size={28} label="Loading metrics…" />
        </div>
      ) : overviewQuery.isError ? (
        <Alert variant="error">We couldn’t load the analytics overview. Please try again.</Alert>
      ) : o ? (
        <>
          {/* KPI grid */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard
              icon={Users}
              tone="brand"
              label="Users"
              value={formatNumber(o.totalUsers)}
              sub={`${formatNumber(o.activeUsers)} active`}
            />
            <StatCard
              icon={FileText}
              tone="info"
              label="Total returns"
              value={formatNumber(o.totalReturns)}
              sub={`${formatNumber(o.returnsFiled)} filed`}
            />
            <StatCard
              icon={FileClock}
              tone="payable"
              label="In progress"
              value={formatNumber(o.returnsInProgress)}
              sub="Returns being prepared"
            />
            <StatCard
              icon={ClipboardCheck}
              tone="info"
              label="Under CA review"
              value={formatNumber(o.returnsUnderCaReview)}
              sub="Awaiting reviewer"
            />
            <StatCard
              icon={IndianRupee}
              tone="money"
              label="Revenue (this month)"
              value={formatInr(o.revenueThisMonthNet)}
              sub={`${formatInr(o.lifetimeRevenueNet)} lifetime`}
            />
            <StatCard
              icon={IndianRupee}
              tone="money"
              label="Paid payments"
              value={formatNumber(o.paidPayments)}
              sub="Captured transactions"
            />
            <StatCard
              icon={FolderCheck}
              tone="payable"
              label="Docs awaiting review"
              value={formatNumber(o.documentsAwaitingReview)}
              sub="HITL verification queue"
            />
            <StatCard
              icon={UserPlus}
              tone="brand"
              label="Open leads"
              value={formatNumber(o.openLeads)}
              sub="In the CRM pipeline"
            />
          </div>

          {/* Revenue + filings */}
          <div className="grid gap-6 lg:grid-cols-2">
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <div>
                  <CardTitle>Net revenue trend</CardTitle>
                  <CardDescription>Monthly captured revenue (net of GST).</CardDescription>
                </div>
                <Link
                  href="/admin/analytics"
                  className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700"
                >
                  Details
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Link>
              </CardHeader>
              <CardContent>
                {revenueQuery.isLoading ? (
                  <div className="flex h-[120px] items-center justify-center">
                    <Spinner label="Loading…" />
                  </div>
                ) : revenueQuery.isError ? (
                  <Alert variant="error">Could not load revenue.</Alert>
                ) : (
                  <>
                    <div className="mb-2 flex items-baseline gap-2">
                      <span className="text-2xl font-semibold text-ink-900">
                        {formatInr(revenueQuery.data?.totalNet ?? 0)}
                      </span>
                      <span className="inline-flex items-center gap-1 text-xs font-medium text-money-700">
                        <TrendingUp className="h-3.5 w-3.5" aria-hidden="true" />
                        {formatNumber(revenueQuery.data?.totalPayments ?? 0)} payments
                      </span>
                    </div>
                    <Sparkline
                      values={(revenueQuery.data?.series ?? []).map((p) => toNumber(p.netAmount))}
                      ariaLabel="Monthly net revenue trend"
                    />
                    <div className="mt-1 flex justify-between text-xs text-ink-400">
                      <span>{revenueQuery.data?.series[0]?.period ?? ''}</span>
                      <span>
                        {revenueQuery.data?.series[revenueQuery.data.series.length - 1]?.period ??
                          ''}
                      </span>
                    </div>
                  </>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Filings by status</CardTitle>
                <CardDescription>
                  {formatNumber(filingsQuery.data?.filedReturns ?? 0)} filed of{' '}
                  {formatNumber(filingsQuery.data?.totalReturns ?? 0)} returns.
                </CardDescription>
              </CardHeader>
              <CardContent>
                {filingsQuery.isLoading ? (
                  <div className="flex h-[120px] items-center justify-center">
                    <Spinner label="Loading…" />
                  </div>
                ) : filingsQuery.isError ? (
                  <Alert variant="error">Could not load the filing funnel.</Alert>
                ) : (
                  <BarChart
                    data={(filingsQuery.data?.byStatus ?? []).map((b) => ({
                      label: humanizeStatus(b.key),
                      value: b.count,
                    }))}
                    format={(v) => formatNumber(v)}
                    emptyLabel="No returns yet"
                  />
                )}
              </CardContent>
            </Card>
          </div>
        </>
      ) : null}
    </div>
  );
}

/** Operational shortcuts shown to Ops users (who can't see revenue analytics). */
function OpsQuickLinks() {
  const links = [
    {
      href: '/admin/returns',
      title: 'Returns board',
      body: 'Triage filings, assign returns to CAs and clear the document-verification queue.',
      icon: FileText,
    },
    {
      href: '/admin/users',
      title: 'Users',
      body: 'Search accounts, manage status and assign roles.',
      icon: Users,
    },
    {
      href: '/admin/leads',
      title: 'CRM pipeline',
      body: 'Work the lead funnel and log activities.',
      icon: UserPlus,
    },
    {
      href: '/admin/audit',
      title: 'Audit log',
      body: 'Review the append-only trail of back-office actions.',
      icon: ClipboardCheck,
    },
  ];
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {links.map((l) => (
        <Link key={l.href} href={l.href} className="group">
          <Card className="h-full p-5 transition-colors group-hover:border-brand-300">
            <div className="flex items-start gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600">
                <l.icon className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <p className="font-semibold text-ink-900">{l.title}</p>
                <p className="mt-1 text-sm text-ink-500">{l.body}</p>
              </div>
            </div>
          </Card>
        </Link>
      ))}
    </div>
  );
}

/** Split a PascalCase status key into spaced words ("UnderCaReview" → "Under Ca Review"). */
function humanizeStatus(key: string): string {
  return key.replace(/([a-z])([A-Z])/g, '$1 $2');
}
