'use client';

// ---------------------------------------------------------------------------
// WizardLayout — the shared chrome for every filing step:
//   • sticky Stepper (Personal..File) with click-back to reachable steps
//   • an autosave status pill ("Saving…" / "Saved")
//   • the step body
//   • a sticky footer with Back / primary action (each step supplies its CTA)
// The layout is presentational; data + nav come from WizardContext.
// ---------------------------------------------------------------------------

import { type ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import { Check, CloudOff, Loader2, Lock } from 'lucide-react';
import { Stepper, type StepperStep, Badge } from '@/components/ui';
import { formatAssessmentYear } from '@/lib/format';
import { formatItrType } from '@/features/returns/helpers';
import { WIZARD_STEPS, STEP_LABEL_KEY, stepIndex, type WizardStepSlug } from '../steps';
import { isStepReachable } from '../useReturn';
import { useWizard } from '../WizardContext';

function AutosavePill() {
  const t = useTranslations('wizard');
  const { saveState } = useWizard();
  if (saveState === 'idle') return null;
  if (saveState === 'saving') {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-ink-500">
        <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
        {t('autosaveSaving')}
      </span>
    );
  }
  if (saveState === 'error') {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-red-600">
        <CloudOff className="h-3.5 w-3.5" aria-hidden="true" />
        {t('autosaveError')}
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-money-700">
      <Check className="h-3.5 w-3.5" aria-hidden="true" />
      {t('autosaveSavedShort')}
    </span>
  );
}

export function WizardLayout({ children }: { children: ReactNode }) {
  const t = useTranslations('wizard');
  const { step, detail, locked, goToStep } = useWizard();

  const steps: StepperStep[] = WIZARD_STEPS.map((slug) => ({
    key: slug,
    label: t(`step.${STEP_LABEL_KEY[slug as WizardStepSlug]}`),
  }));

  const current = stepIndex(step);

  return (
    <div className="mx-auto w-full max-w-3xl pb-28">
      {/* Sticky header: title + stepper (sits just below the 4rem Topbar). */}
      <div className="sticky top-16 z-20 -mx-4 mb-6 border-b border-ink-100 bg-ink-50/95 px-4 pb-4 pt-4 backdrop-blur sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-semibold text-ink-900">{t('title')}</h1>
            <Badge tone="neutral">{formatAssessmentYear(detail.assessmentYear)}</Badge>
            {detail.itrType && <Badge tone="brand">{formatItrType(detail.itrType)}</Badge>}
            {locked && (
              <Badge tone="success" className="gap-1">
                <Lock className="h-3 w-3" aria-hidden="true" /> {t('filed')}
              </Badge>
            )}
          </div>
          <AutosavePill />
        </div>
        <Stepper
          steps={steps}
          current={current}
          onStepClick={(i, s) => {
            if (isStepReachable(s.key as WizardStepSlug, detail)) {
              goToStep(s.key as WizardStepSlug);
            }
          }}
        />
      </div>

      {children}
    </div>
  );
}
