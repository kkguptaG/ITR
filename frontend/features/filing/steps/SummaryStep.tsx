'use client';

// ---------------------------------------------------------------------------
// Step 6 — Summary. Runs POST /tax/compute (persists the computation), renders
// the TaxSummaryPanel for the selected regime (gross -> deductions -> taxable ->
// tax -> refund/payable) with the trace expandable, and surfaces pre-file
// validation findings (POST /returns/{id}:validate). Proceeds to Payment.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Info } from 'lucide-react';
import { Alert, Button, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { cn } from '@/lib/utils';
import { computeTax, filingKeys, validateReturn } from '../api';
import type { TaxComputationResultDto } from '../types';
import { useWizard } from '../WizardContext';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { TaxSummaryPanel } from '../components/TaxSummaryPanel';
import { PrepaidTaxesCard } from '../components/PrepaidTaxesCard';

export function SummaryStep() {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const { returnId, detail, goNext } = useWizard();

  const computeQuery = useQuery({
    queryKey: filingKeys.compute(returnId),
    queryFn: () => computeTax({ returnId }),
    retry: false,
    staleTime: 10_000,
  });

  const validateQuery = useQuery({
    queryKey: [...filingKeys.compute(returnId), 'validate'],
    queryFn: () => validateReturn(returnId),
    retry: false,
    staleTime: 10_000,
  });

  // Pick the panel for the user's chosen regime; fall back to the recommended one.
  const chosen: TaxComputationResultDto | undefined = (() => {
    const data = computeQuery.data;
    if (!data) return undefined;
    const regime = detail.regime ?? data.recommendedRegime;
    return regime === 'Old' ? data.old : data.new;
  })();

  const errorMessage =
    computeQuery.error instanceof ApiError
      ? (computeQuery.error.problem.detail ?? computeQuery.error.message)
      : computeQuery.error
        ? t('computeError')
        : null;

  const findings = validateQuery.data?.findings ?? [];
  const blockers = findings.filter((f) => f.severity === 'block');
  const warnings = findings.filter((f) => f.severity !== 'block');

  return (
    <>
      <WizardStep title={t('summaryTitle')} description={t('summarySubtitle')}>
        <PrepaidTaxesCard returnId={returnId} detail={detail} />
        {computeQuery.isLoading ? (
          <div className="flex flex-col items-center gap-2 py-10 text-sm text-ink-500">
            <Spinner />
            {t('computing')}
          </div>
        ) : errorMessage ? (
          <Alert variant="error" title={t('computeError')}>
            {errorMessage}
            <div className="mt-2">
              <Button variant="outline" size="sm" onClick={() => void computeQuery.refetch()}>
                {tc('retry')}
              </Button>
            </div>
          </Alert>
        ) : chosen ? (
          <>
            <TaxSummaryPanel comp={chosen} />

            {blockers.length > 0 && (
              <FindingList
                tone="error"
                icon={<AlertTriangle className="h-4 w-4" aria-hidden="true" />}
                title={t('blockersTitle')}
                items={blockers.map((f) => f.message)}
              />
            )}
            {warnings.length > 0 && (
              <FindingList
                tone="warning"
                icon={<Info className="h-4 w-4" aria-hidden="true" />}
                title={t('warningsTitle')}
                items={warnings.map((f) => f.message)}
              />
            )}
          </>
        ) : null}
      </WizardStep>

      <WizardFooter
        primary={
          <Button type="button" onClick={goNext} disabled={!chosen || blockers.length > 0}>
            {t('proceedToPayment')}
          </Button>
        }
      />
    </>
  );
}

function FindingList({
  tone,
  icon,
  title,
  items,
}: {
  tone: 'warning' | 'error';
  icon: React.ReactNode;
  title: string;
  items: string[];
}) {
  return (
    <div
      className={cn(
        'rounded-xl border p-3.5 text-sm',
        tone === 'error' ? 'border-red-200 bg-red-50 text-red-900' : 'border-payable-200 bg-payable-50 text-payable-800',
      )}
    >
      <div className="mb-1 flex items-center gap-1.5 font-semibold">
        {icon}
        {title}
      </div>
      <ul className="ml-5 list-disc space-y-0.5">
        {items.map((m, i) => (
          <li key={i}>{m}</li>
        ))}
      </ul>
    </div>
  );
}
