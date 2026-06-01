'use client';

// ---------------------------------------------------------------------------
// Step 5 — Regime. Runs POST /tax/regime-compare (computes + persists both
// regimes) and renders the old-vs-new RegimeCompareCard with the recommended
// option + savings delta. Choosing a regime PATCHes it onto the return and
// advances to the summary.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Alert, Button, Spinner } from '@/components/ui';
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

  const compareQuery = useQuery({
    queryKey: filingKeys.regimeCompare(returnId),
    queryFn: () => regimeCompare(returnId),
    retry: false,
    staleTime: 10_000,
  });

  const chooseMutation = useMutation({
    mutationFn: (regime: Regime) => updateReturn(returnId, { regime }),
    onMutate: () => setSaveState('saving'),
    onSuccess: () => {
      setSaveState('saved');
      invalidate();
      goNext();
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
