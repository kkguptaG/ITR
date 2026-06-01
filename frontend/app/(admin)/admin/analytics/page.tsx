'use client';

// ---------------------------------------------------------------------------
// /admin/analytics — revenue + filings analytics.
//   • Revenue: granularity toggle (day/week/month) → GET /admin/analytics/revenue.
//     Totals + sparkline trend + per-period breakdown bars.
//   • Filings: GET /admin/analytics/filings → funnel by status / ITR type / regime.
// Gated (UI) to Admin/SuperAdmin; the API enforces the tighter analytics RBAC.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { IndianRupee, Receipt, FileText, Percent } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Spinner,
  Alert,
} from '@/components/ui';
import { formatInr, formatNumber, toNumber } from '@/lib/format';
import {
  PageHeader,
  StatCard,
  Sparkline,
  BarChart,
  adminKeys,
  getRevenue,
  getFilings,
  type RevenueGranularity,
} from '@/features/admin';

const GRANULARITIES: { value: RevenueGranularity; label: string }[] = [
  { value: 'day', label: 'Daily' },
  { value: 'week', label: 'Weekly' },
  { value: 'month', label: 'Monthly' },
];

function humanize(s: string): string {
  return s.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export default function AdminAnalyticsPage() {
  const [granularity, setGranularity] = useState<RevenueGranularity>('month');

  const revenueQuery = useQuery({
    queryKey: adminKeys.revenue(granularity),
    queryFn: () => getRevenue(granularity),
  });

  const filingsQuery = useQuery({
    queryKey: adminKeys.filings(),
    queryFn: getFilings,
  });

  const rev = revenueQuery.data;
  const filings = filingsQuery.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Analytics"
        subtitle="Revenue performance and the filing funnel."
      />

      {/* Revenue */}
      <section className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold text-ink-900">Revenue</h2>
          <div className="inline-flex rounded-xl bg-ink-100 p-1" role="group" aria-label="Granularity">
            {GRANULARITIES.map((g) => (
              <button
                key={g.value}
                type="button"
                onClick={() => setGranularity(g.value)}
                aria-pressed={granularity === g.value}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                  granularity === g.value
                    ? 'bg-white text-ink-900 shadow-sm'
                    : 'text-ink-500 hover:text-ink-800'
                }`}
              >
                {g.label}
              </button>
            ))}
          </div>
        </div>

        {revenueQuery.isLoading ? (
          <div className="flex min-h-[24vh] items-center justify-center">
            <Spinner size={28} label="Loading revenue…" />
          </div>
        ) : revenueQuery.isError ? (
          <Alert variant="error">We couldn’t load revenue analytics. Please try again.</Alert>
        ) : rev ? (
          <>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard
                icon={IndianRupee}
                tone="money"
                label="Net revenue"
                value={formatInr(rev.totalNet)}
                sub="Net of GST"
              />
              <StatCard
                icon={Receipt}
                tone="brand"
                label="Gross revenue"
                value={formatInr(rev.totalGross)}
                sub="Incl. GST"
              />
              <StatCard
                icon={Percent}
                tone="info"
                label="GST collected"
                value={formatInr(rev.totalGst)}
              />
              <StatCard
                icon={FileText}
                tone="neutral"
                label="Payments"
                value={formatNumber(rev.totalPayments)}
                sub="Captured transactions"
              />
            </div>

            <Card>
              <CardHeader>
                <CardTitle>Net revenue trend</CardTitle>
                <CardDescription>
                  {GRANULARITIES.find((g) => g.value === granularity)?.label} buckets across the
                  selected window.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Sparkline
                  values={rev.series.map((p) => toNumber(p.netAmount))}
                  height={140}
                  ariaLabel="Net revenue trend"
                />
                <div className="mt-1 flex justify-between text-xs text-ink-400">
                  <span>{rev.series[0]?.period ?? ''}</span>
                  <span>{rev.series[rev.series.length - 1]?.period ?? ''}</span>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Revenue by period</CardTitle>
                <CardDescription>Net revenue and payment count per bucket.</CardDescription>
              </CardHeader>
              <CardContent>
                <BarChart
                  data={rev.series.map((p) => ({
                    label: p.period,
                    value: toNumber(p.netAmount),
                    hint: `${formatNumber(p.paymentCount)} pmt`,
                  }))}
                  format={(v) => formatInr(v)}
                  barClassName="bg-money-500"
                  emptyLabel="No revenue in this window"
                />
              </CardContent>
            </Card>
          </>
        ) : null}
      </section>

      {/* Filings funnel */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold text-ink-900">Filings</h2>

        {filingsQuery.isLoading ? (
          <div className="flex min-h-[20vh] items-center justify-center">
            <Spinner size={28} label="Loading filings…" />
          </div>
        ) : filingsQuery.isError ? (
          <Alert variant="error">We couldn’t load filing analytics. Please try again.</Alert>
        ) : filings ? (
          <>
            <div className="grid gap-4 sm:grid-cols-2">
              <StatCard
                icon={FileText}
                tone="brand"
                label="Total returns"
                value={formatNumber(filings.totalReturns)}
              />
              <StatCard
                icon={FileText}
                tone="money"
                label="Filed returns"
                value={formatNumber(filings.filedReturns)}
                sub={
                  filings.totalReturns > 0
                    ? `${Math.round((filings.filedReturns / filings.totalReturns) * 100)}% of total`
                    : undefined
                }
              />
            </div>

            <div className="grid gap-6 lg:grid-cols-3">
              <Card>
                <CardHeader>
                  <CardTitle>By status</CardTitle>
                </CardHeader>
                <CardContent>
                  <BarChart
                    data={filings.byStatus.map((b) => ({ label: humanize(b.key), value: b.count }))}
                    format={(v) => formatNumber(v)}
                    emptyLabel="No returns yet"
                  />
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>By ITR type</CardTitle>
                </CardHeader>
                <CardContent>
                  <BarChart
                    data={filings.byItrType.map((b) => ({ label: b.key, value: b.count }))}
                    format={(v) => formatNumber(v)}
                    barClassName="bg-sky-500"
                    emptyLabel="No returns yet"
                  />
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>By regime</CardTitle>
                </CardHeader>
                <CardContent>
                  <BarChart
                    data={filings.byRegime.map((b) => ({ label: `${b.key} regime`, value: b.count }))}
                    format={(v) => formatNumber(v)}
                    barClassName="bg-payable-500"
                    emptyLabel="No returns yet"
                  />
                </CardContent>
              </Card>
            </div>
          </>
        ) : null}
      </section>
    </div>
  );
}
