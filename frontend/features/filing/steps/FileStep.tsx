'use client';

// ---------------------------------------------------------------------------
// Step 8 — File. The taxpayer chooses to either:
//   • Self-file  -> POST /returns/{id}:submit (the stubbed ERI returns an
//     acknowledgment number + ITR-V); we show the ack + PDF download buttons.
//   • CA review  -> record the choice (filingMode=ca in answersJson) so an
//     operator can pick the Paid return up; we show a "submitted for review"
//     confirmation. (Assignment itself is an operator action per RBAC.)
// Re-entry is idempotent: a Filed return shows its existing acknowledgment.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation } from '@tanstack/react-query';
import {
  CheckCircle2,
  Download,
  FileCheck2,
  ShieldQuestion,
  UserCheck,
} from 'lucide-react';
import { Alert, Badge, Button, Card } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { cn } from '@/lib/utils';
import { formatDateTime } from '@/lib/format';
import { submitReturn, updateReturn } from '../api';
import type { FilingMode, SubmitReturnResponse } from '../types';
import { downloadAcknowledgment, downloadComputation } from '../download';
import { useWizard } from '../WizardContext';
import { useInvalidateReturn } from '../useReturn';
import { useQuery } from '@tanstack/react-query';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { ItrJsonPanel } from '../components/ItrJsonPanel';
import { ReconciliationCard } from '@/features/reconciliation';
import { listItrJsonForReturn } from '../itr-json';

export function FileStep() {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const { returnId, detail } = useWizard();
  const invalidate = useInvalidateReturn(returnId);

  const isFiled = detail.status === 'Filed' || detail.status === 'Processed';
  const isUnderReview = detail.status === 'UnderCaReview';

  const [mode, setMode] = useState<FilingMode>('self');
  const [ack, setAck] = useState<SubmitReturnResponse | null>(
    isFiled && detail.acknowledgmentNumber
      ? {
          id: detail.id,
          status: detail.status,
          acknowledgmentNumber: detail.acknowledgmentNumber,
          submittedAt: detail.submittedAt ?? new Date().toISOString(),
          versionNo: 0,
          snapshotHash: '',
        }
      : null,
  );
  const [caRequested, setCaRequested] = useState(isUnderReview);

  // Gate the "e-file now" button on having a Valid ITR JSON artifact — users shouldn't be
  // able to submit without first generating and validating the JSON.
  const itrJsonQuery = useQuery({
    queryKey: ['itr-json', 'forReturn', returnId],
    queryFn: () => listItrJsonForReturn(returnId),
    staleTime: 15_000,
  });
  const hasValidJson = (itrJsonQuery.data ?? []).some((a) => a.status === 'Valid');

  const submitMutation = useMutation({
    mutationFn: () => submitReturn(returnId),
    onSuccess: (res) => {
      setAck(res);
      invalidate();
    },
  });

  const caMutation = useMutation({
    mutationFn: () => {
      const answers = safeParse(detail.answersJson);
      return updateReturn(returnId, {
        answersJson: JSON.stringify({ ...answers, filingMode: 'ca' }),
      });
    },
    onSuccess: () => {
      setCaRequested(true);
      invalidate();
    },
  });

  const submitError =
    submitMutation.error instanceof ApiError
      ? (submitMutation.error.problem.detail ?? submitMutation.error.message)
      : submitMutation.error
        ? tc('retry')
        : null;

  // ---- Terminal: filed (self-file complete) ----
  if (ack) {
    return (
      <WizardStep title={t('fileTitle')} description={undefined}>
        <Card className="overflow-hidden">
          <div className="flex flex-col items-center gap-2 bg-money-50 px-6 py-8 text-center">
            <FileCheck2 className="h-10 w-10 text-money-600" aria-hidden="true" />
            <h3 className="text-lg font-semibold text-ink-900">{t('filedTitle')}</h3>
            <p className="text-sm text-ink-600">{t('filedBody')}</p>
            <div className="mt-2 rounded-xl bg-white px-4 py-2 shadow-sm">
              <div className="text-xs text-ink-400">{t('acknowledgmentNo')}</div>
              <div className="font-mono text-base font-semibold tracking-wide text-ink-900">
                {ack.acknowledgmentNumber}
              </div>
            </div>
            {ack.submittedAt && (
              <div className="text-xs text-ink-400">{formatDateTime(ack.submittedAt)}</div>
            )}
          </div>
          <div className="flex flex-col gap-2 p-5 sm:flex-row">
            <Button variant="outline" className="flex-1" onClick={() => void downloadAcknowledgment(returnId)}>
              <Download className="h-4 w-4" aria-hidden="true" />
              {t('downloadAck')}
            </Button>
            <Button variant="outline" className="flex-1" onClick={() => void downloadComputation(returnId)}>
              <Download className="h-4 w-4" aria-hidden="true" />
              {t('downloadComputation')}
            </Button>
          </div>
        </Card>
        <ItrJsonPanel returnId={returnId} />
        <WizardFooter
          hideBack
          primary={
            <Button type="button" onClick={() => (window.location.href = `/returns/${returnId}`)}>
              {t('viewReturn')}
            </Button>
          }
        />
      </WizardStep>
    );
  }

  // ---- Terminal: submitted for CA review ----
  if (caRequested) {
    return (
      <WizardStep title={t('fileTitle')} description={undefined}>
        <Card className="flex flex-col items-center gap-2 px-6 py-8 text-center">
          <UserCheck className="h-10 w-10 text-brand-600" aria-hidden="true" />
          <h3 className="text-lg font-semibold text-ink-900">{t('caRequestedTitle')}</h3>
          <p className="max-w-md text-sm text-ink-600">{t('caRequestedBody')}</p>
          <Badge tone="info" className="mt-1">
            {t('statusUnderReview')}
          </Badge>
        </Card>
        <WizardFooter
          hideBack
          primary={
            <Button type="button" onClick={() => (window.location.href = `/returns/${returnId}`)}>
              {t('viewReturn')}
            </Button>
          }
        />
      </WizardStep>
    );
  }

  // ---- Choice: self-file or CA review ----
  return (
    <>
      <WizardStep title={t('fileTitle')} description={t('fileSubtitle')}>
        <div className="grid gap-4 sm:grid-cols-2">
          <ChoiceCard
            active={mode === 'self'}
            onSelect={() => setMode('self')}
            icon={<FileCheck2 className="h-6 w-6" aria-hidden="true" />}
            title={t('selfFileTitle')}
            body={t('selfFileBody')}
            badge={t('selfFileBadge')}
          />
          <ChoiceCard
            active={mode === 'ca'}
            onSelect={() => setMode('ca')}
            icon={<ShieldQuestion className="h-6 w-6" aria-hidden="true" />}
            title={t('caReviewTitle')}
            body={t('caReviewBody')}
            badge={t('caReviewBadge')}
          />
        </div>

        {submitError && <Alert variant="error">{submitError}</Alert>}

        <Alert variant="info">
          {mode === 'self' ? t('selfFileNote') : t('caReviewNote')}
        </Alert>

        {/* Pre-filing cross-check against the department's AIS / 26AS — under-reporting is the top
            cause of a §143(1) notice, so surface it right before the taxpayer commits to filing. */}
        <ReconciliationCard returnId={returnId} />

        <ItrJsonPanel returnId={returnId} />
      </WizardStep>

      <WizardFooter
        primary={
          mode === 'self' ? (
            <Button type="button" onClick={() => submitMutation.mutate()} loading={submitMutation.isPending}
              disabled={!hasValidJson}
              title={!hasValidJson ? "Generate and validate the ITR JSON (below) before filing" : undefined}>
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
              {t('efileNow')}
            </Button>
          ) : (
            <Button type="button" onClick={() => caMutation.mutate()} loading={caMutation.isPending}>
              <UserCheck className="h-4 w-4" aria-hidden="true" />
              {t('sendToCa')}
            </Button>
          )
        }
      />
    </>
  );
}

function ChoiceCard({
  active,
  onSelect,
  icon,
  title,
  body,
  badge,
}: {
  active: boolean;
  onSelect: () => void;
  icon: React.ReactNode;
  title: string;
  body: string;
  badge: string;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={active}
      className={cn(
        'flex flex-col items-start gap-2 rounded-2xl border p-5 text-left shadow-card transition-colors',
        active ? 'border-brand-500 ring-2 ring-brand-500' : 'border-ink-200 hover:border-brand-300',
      )}
    >
      <span className={cn('rounded-xl p-2', active ? 'bg-brand-100 text-brand-700' : 'bg-ink-100 text-ink-500')}>
        {icon}
      </span>
      <span className="flex items-center gap-2">
        <span className="font-semibold text-ink-900">{title}</span>
        <Badge tone={active ? 'brand' : 'neutral'}>{badge}</Badge>
      </span>
      <span className="text-sm text-ink-500">{body}</span>
    </button>
  );
}

function safeParse(json: string | null | undefined): Record<string, unknown> {
  if (!json) return {};
  try {
    const v = JSON.parse(json);
    return typeof v === 'object' && v ? (v as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}
