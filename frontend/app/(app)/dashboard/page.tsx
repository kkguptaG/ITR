'use client';

// ---------------------------------------------------------------------------
// /dashboard — the signed-in home, redesigned as a filing cockpit:
//   • greeting + return-type pill
//   • four status cards (filing gauge · refund · due date · filing mode)
//   • quick actions · income/deductions/tax summary · smart insights + tasks
//   • document status · recent returns · trust footer
// Driven by the latest return's real computed result + deductions + documents.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { Plus, FileText, ShieldCheck, TrendingUp, Headphones, CalendarCheck } from 'lucide-react';
import { Button, Card, CardHeader, CardTitle, CardContent, Spinner, Alert } from '@/components/ui';
import { useAuth } from '@/lib/auth';
import {
  listReturns,
  getActiveAssessmentYear,
  returnsKeys,
  NewReturnDialog,
} from '@/features/returns';
import { formatAssessmentYear } from '@/lib/format';
import { formatItrType } from '@/features/returns/helpers';
import { computeTax, listDeductions, filingKeys } from '@/features/filing/api';
import { listDocuments } from '@/features/documents/api';
import { deadlineFor } from '@/features/dashboard/deadlines';
import { EVerifyReminder } from '@/features/dashboard/components/EVerifyReminder';
import { StatusCards } from '@/features/dashboard/components/StatusCards';
import { QuickActionsGrid } from '@/features/dashboard/components/QuickActionsGrid';
import { DashboardSummary } from '@/features/dashboard/components/DashboardSummary';
import { InsightsAndTasks } from '@/features/dashboard/components/InsightsAndTasks';
import { DocumentStatusCard } from '@/features/dashboard/components/DocumentStatusCard';
import { RecentReturns } from '@/features/dashboard/components/RecentReturns';

const RECENT_PAGE_SIZE = 5;

export default function DashboardPage() {
  const t = useTranslations('dashboard');
  const tr = useTranslations('returns');
  const th = useTranslations('home');
  const { user } = useAuth();
  const [dialogOpen, setDialogOpen] = useState(false);

  const listQuery = useQuery({
    queryKey: returnsKeys.list({ page: 1, pageSize: RECENT_PAGE_SIZE }),
    queryFn: () => listReturns({ page: 1, pageSize: RECENT_PAGE_SIZE }),
  });
  const items = listQuery.data?.items ?? [];
  const total = listQuery.data?.total ?? 0;
  const latest = items[0];

  const ayQuery = useQuery({
    queryKey: returnsKeys.activeAy,
    queryFn: getActiveAssessmentYear,
    staleTime: 60 * 60_000,
  });

  const computeQuery = useQuery({
    queryKey: latest ? filingKeys.compute(latest.id) : ['filing', 'compute', 'none'],
    queryFn: () => computeTax({ returnId: latest!.id }),
    enabled: !!latest,
  });
  const deductionsQuery = useQuery({
    queryKey: latest ? filingKeys.deductions(latest.id) : ['filing', 'deductions', 'none'],
    queryFn: () => listDeductions(latest!.id),
    enabled: !!latest,
  });
  const docsQuery = useQuery({
    queryKey: ['documents', 'dashboard'],
    queryFn: () => listDocuments({ page: 1, pageSize: 12 }),
  });

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

  const compute = computeQuery.data;
  const result = compute ? (compute.recommendedRegime === 'Old' ? compute.old : compute.new) : null;
  const deadline = deadlineFor(ayQuery.data?.assessmentYear ?? latest?.assessmentYear ?? '');
  const deductions = deductionsQuery.data ?? [];
  const documents = docsQuery.data?.items ?? [];

  return (
    <div className="space-y-6">
      {/* Greeting + return-type pill + CTA */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-ink-900">{greeting}</h1>
          <p className="mt-1 text-sm text-ink-500">{tr('dashboardSubtitle')}</p>
        </div>
        <div className="flex items-center gap-3">
          {latest?.itrType && (
            <span className="hidden items-center gap-2 rounded-full border border-ink-200 bg-white px-3 py-1.5 text-sm sm:inline-flex">
              <span className="text-ink-500">{th('returnLabel')}</span>
              <span className="font-semibold text-ink-900">{formatItrType(latest.itrType)}</span>
              <span className="text-ink-400">·</span>
              <span className="text-ink-600">{formatAssessmentYear(latest.assessmentYear)}</span>
            </span>
          )}
          <Button onClick={() => setDialogOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            {tr('startNewReturn')}
          </Button>
        </div>
      </div>

      {listQuery.isError && <Alert variant="error">{tr('listError')}</Alert>}

      {!latest ? (
        <GetStarted onNew={() => setDialogOpen(true)} title={tr('getStarted')} body={tr('getStartedBody')} cta={tr('startCta')} />
      ) : (
        <>
          <EVerifyReminder items={items} />

          <StatusCards
            latest={latest}
            refundOrPayable={result?.refundOrPayable ?? latest.refundOrPayable}
            regime={result?.regime ?? latest.regime}
            deadline={deadline}
          />

          <QuickActionsGrid returnId={latest.id} />

          {result ? (
            <>
              <DashboardSummary result={result} deductions={deductions} returnId={latest.id} />
              <InsightsAndTasks result={result} latest={latest} />
            </>
          ) : computeQuery.isLoading ? (
            <Card className="flex items-center justify-center py-10">
              <Spinner label="Computing your return…" />
            </Card>
          ) : null}

          <div className="grid gap-4 lg:grid-cols-2">
            <DocumentStatusCard documents={documents} />
            <RecentReturns items={items} total={total} onNewReturn={() => setDialogOpen(true)} />
          </div>

          <TrustFooter />
        </>
      )}

      <NewReturnDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </div>
  );
}

function GetStarted({ onNew, title, body, cta }: { onNew: () => void; title: string; body: string; cta: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-start gap-3 rounded-xl bg-brand-50 p-4">
          <FileText className="mt-0.5 h-5 w-5 shrink-0 text-brand-600" aria-hidden="true" />
          <p className="text-sm text-brand-900">{body}</p>
        </div>
        <Button fullWidth onClick={onNew}>
          <Plus className="h-4 w-4" aria-hidden="true" />
          {cta}
        </Button>
      </CardContent>
    </Card>
  );
}

const TRUST = [
  { icon: ShieldCheck, key: 'Secure' },
  { icon: TrendingUp, key: 'Refund' },
  { icon: Headphones, key: 'Expert' },
  { icon: CalendarCheck, key: 'Ontime' },
] as const;

function TrustFooter() {
  const th = useTranslations('home');
  return (
    <div className="grid gap-4 rounded-2xl border border-ink-200 bg-white p-5 shadow-card sm:grid-cols-2 lg:grid-cols-4">
      {TRUST.map(({ icon: Icon, key }) => (
        <div key={key} className="flex items-start gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-money-50 text-money-600">
            <Icon className="h-5 w-5" aria-hidden="true" />
          </span>
          <div>
            <p className="text-sm font-semibold text-ink-900">{th(`trust${key}Title`)}</p>
            <p className="text-xs text-ink-500">{th(`trust${key}Sub`)}</p>
          </div>
        </div>
      ))}
    </div>
  );
}
