'use client';

// ---------------------------------------------------------------------------
// Step 5 — Regime. Runs POST /tax/regime-compare (computes + persists both
// regimes) and renders the old-vs-new RegimeCompareCard with the recommended
// option + savings delta. Choosing a regime PATCHes it onto the return and
// advances to the summary.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Alert, Button, Input, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import type { Regime } from '@/lib/api-types';
import { filingKeys, regimeCompare, updateReturn } from '../api';
import { useWizard } from '../WizardContext';
import { useInvalidateReturn } from '../useReturn';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { RegimeCompareCard } from '../components/RegimeCompareCard';

export function RegimeStep() {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const { returnId, detail, goNext, setSaveState } = useWizard();
  const invalidate = useInvalidateReturn(returnId);

  // Form 10-IEA is required only for a business/professional taxpayer (ITR-3/4) opting OUT to the old regime.
  const isBusinessForm = detail.itrType === 'ITR3' || detail.itrType === 'ITR4';
  const [ackNo, setAckNo] = useState(detail.form10IeaAckNumber ?? '');
  const [f10Date, setF10Date] = useState(detail.form10IeaDate ?? '');
  const ackValid = ackNo.trim() === '' || /^[1-9][0-9]{14}$/.test(ackNo.trim());

  const compareQuery = useQuery({
    queryKey: filingKeys.regimeCompare(returnId),
    queryFn: () => regimeCompare(returnId),
    retry: false,
    staleTime: 10_000,
  });

  const chooseMutation = useMutation({
    mutationFn: (regime: Regime) => updateReturn(returnId, { regime }),
    onMutate: () => setSaveState('saving'),
    onSuccess: (_data, regime) => {
      setSaveState('saved');
      invalidate();
      // A business taxpayer choosing OLD stays on this step to record Form 10-IEA before continuing.
      if (!(regime === 'Old' && isBusinessForm)) goNext();
    },
    onError: () => setSaveState('error'),
  });

  const form10IeaMutation = useMutation({
    mutationFn: () =>
      updateReturn(returnId, {
        form10IeaAckNumber: ackNo.trim() || null,
        form10IeaDate: f10Date || null,
      }),
    onMutate: () => setSaveState('saving'),
    onSuccess: () => {
      setSaveState('saved');
      invalidate();
    },
    onError: () => setSaveState('error'),
  });

  const errorMessage =
    compareQuery.error instanceof ApiError
      ? (compareQuery.error.problem.detail ?? compareQuery.error.message)
      : compareQuery.error
        ? t('computeError')
        : null;

  return (
    <>
      <WizardStep title={t('regimeTitle')} description={t('regimeSubtitle')}>
        {compareQuery.isLoading ? (
          <div className="flex flex-col items-center gap-2 py-10 text-sm text-ink-500">
            <Spinner />
            {t('computingRegimes')}
          </div>
        ) : errorMessage ? (
          <Alert variant="error" title={t('computeError')}>
            {errorMessage}
            <div className="mt-2">
              <Button variant="outline" size="sm" onClick={() => void compareQuery.refetch()}>
                {tc('retry')}
              </Button>
            </div>
          </Alert>
        ) : compareQuery.data ? (
          <RegimeCompareCard
            data={compareQuery.data}
            selected={detail.regime}
            onChoose={(regime) => chooseMutation.mutate(regime)}
          />
        ) : null}

        {detail.regime === 'Old' && isBusinessForm ? (
          <div className="mt-4 rounded-xl border border-brand-200 bg-brand-50/40 p-4">
            <div className="text-sm font-semibold text-ink-800">Form 10-IEA — opting out of the new regime</div>
            <p className="mt-1 text-xs text-ink-600">
              As a business / professional taxpayer, choosing the <strong>old regime</strong> requires filing
              Form 10-IEA on the income-tax portal before the due date. Enter its acknowledgement number and
              filing date so we can quote them in your return (s.115BAC).
            </p>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <label className="block text-sm">
                <span className="mb-1 block font-medium text-ink-700">Acknowledgement number</span>
                <Input
                  value={ackNo}
                  onChange={(e) => setAckNo(e.target.value)}
                  placeholder="15-digit receipt number"
                  inputMode="numeric"
                />
                {!ackValid ? <span className="mt-1 block text-xs text-red-600">Must be a 15-digit number.</span> : null}
              </label>
              <label className="block text-sm">
                <span className="mb-1 block font-medium text-ink-700">Date of filing</span>
                <Input type="date" value={f10Date} onChange={(e) => setF10Date(e.target.value)} />
              </label>
            </div>
            <div className="mt-3">
              <Button
                type="button"
                variant="outline"
                size="sm"
                loading={form10IeaMutation.isPending}
                disabled={!ackValid}
                onClick={() => form10IeaMutation.mutate()}
              >
                Save Form 10-IEA details
              </Button>
            </div>
          </div>
        ) : null}
      </WizardStep>

      <WizardFooter
        primary={
          <Button
            type="button"
            onClick={() => {
              // If a regime is already chosen, just continue; else pick the recommended.
              if (detail.regime) {
                goNext();
              } else if (compareQuery.data) {
                chooseMutation.mutate(compareQuery.data.recommendedRegime);
              }
            }}
            loading={chooseMutation.isPending}
            disabled={!compareQuery.data}
          >
            {detail.regime ? tc('continue') : t('useRecommendedRegime')}
          </Button>
        }
      />
    </>
  );
}
