'use client';

// ---------------------------------------------------------------------------
// WizardRunner — the client entry for /returns/[returnId]/file/[step]. Loads the
// return, guards the step (valid slug + reachable), wires the WizardProvider and
// renders the shared layout + the active step body. If the user deep-links to a
// step they haven't reached yet, we redirect to the furthest reachable step.
// ---------------------------------------------------------------------------

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { AlertCircle } from 'lucide-react';
import { Button, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { isWizardStep, stepHref, type WizardStepSlug } from './steps';
import { useReturnDetail, furthestStep, isStepReachable } from './useReturn';
import { WizardProvider } from './WizardContext';
import { WizardLayout } from './components/WizardLayout';
import { STEP_COMPONENTS } from './steps/index';

export function WizardRunner({ returnId, step }: { returnId: string; step: string }) {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const router = useRouter();
  const detailQuery = useReturnDetail(returnId);
  const detail = detailQuery.data;

  const validStep = isWizardStep(step);

  // Redirect deep-links to unreachable steps back to the furthest reachable one.
  useEffect(() => {
    if (!detail || !validStep) return;
    if (!isStepReachable(step as WizardStepSlug, detail)) {
      router.replace(stepHref(returnId, furthestStep(detail)));
    }
  }, [detail, validStep, step, returnId, router]);

  if (!validStep) {
    return <Centered>{t('unknownStep')}</Centered>;
  }

  if (detailQuery.isLoading) {
    return (
      <Centered>
        <Spinner label={tc('loading')} />
      </Centered>
    );
  }

  if (detailQuery.isError || !detail) {
    const msg =
      detailQuery.error instanceof ApiError
        ? (detailQuery.error.problem.detail ?? detailQuery.error.message)
        : t('loadError');
    return (
      <Centered>
        <AlertCircle className="h-8 w-8 text-red-500" aria-hidden="true" />
        <p className="text-sm text-ink-600">{msg}</p>
        <Button variant="outline" size="sm" onClick={() => void detailQuery.refetch()}>
          {tc('retry')}
        </Button>
      </Centered>
    );
  }

  // Guarded above: while a redirect to a reachable step is in flight, show a spinner.
  if (!isStepReachable(step as WizardStepSlug, detail)) {
    return (
      <Centered>
        <Spinner />
      </Centered>
    );
  }

  const StepBody = STEP_COMPONENTS[step as WizardStepSlug];

  return (
    <WizardProvider returnId={returnId} step={step as WizardStepSlug} detail={detail}>
      <WizardLayout>
        <StepBody />
      </WizardLayout>
    </WizardProvider>
  );
}

function Centered({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 text-center">
      {children}
    </div>
  );
}
