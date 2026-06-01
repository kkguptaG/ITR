// ---------------------------------------------------------------------------
// features/filing/steps.ts
// The canonical wizard step registry: order, slugs, i18n keys, and routing.
// Steps are fixed in order (Personal..File); the Stepper renders them all.
// Conditional *content* (e.g. which income forms to show) is decided per-step
// from the ITR type, not by adding/removing steps — so progress stays stable.
// ---------------------------------------------------------------------------

import type { ItrType } from '@/lib/api-types';

export const WIZARD_STEPS = [
  'personal',
  'documents',
  'income',
  'deductions',
  'regime',
  'summary',
  'payment',
  'file',
] as const;

export type WizardStepSlug = (typeof WIZARD_STEPS)[number];

/** i18n key under the `wizard.step.*` namespace for each slug. */
export const STEP_LABEL_KEY: Record<WizardStepSlug, string> = {
  personal: 'personal',
  documents: 'documents',
  income: 'income',
  deductions: 'deductions',
  regime: 'regime',
  summary: 'summary',
  payment: 'payment',
  file: 'efile',
};

export function isWizardStep(value: string): value is WizardStepSlug {
  return (WIZARD_STEPS as readonly string[]).includes(value);
}

export function stepIndex(slug: WizardStepSlug): number {
  return WIZARD_STEPS.indexOf(slug);
}

export function nextStep(slug: WizardStepSlug): WizardStepSlug | null {
  const i = stepIndex(slug);
  return i >= 0 && i < WIZARD_STEPS.length - 1 ? WIZARD_STEPS[i + 1] : null;
}

export function prevStep(slug: WizardStepSlug): WizardStepSlug | null {
  const i = stepIndex(slug);
  return i > 0 ? WIZARD_STEPS[i - 1] : null;
}

export function stepHref(returnId: string, slug: WizardStepSlug): string {
  return `/returns/${returnId}/file/${slug}`;
}

// ----------------------------------------------------------- ITR-conditional income heads
// Which income heads are relevant for a given ITR form (drives the Income step).
//   ITR-1: salary + one house property + other sources
//   ITR-2: + multiple house properties + capital gains
//   ITR-3: + business/profession (incl. F&O / speculative)
//   ITR-4: salary + one house property + presumptive business (44AD/ADA)

export interface IncomeHeadVisibility {
  salary: boolean;
  houseProperty: boolean;
  capitalGains: boolean;
  business: boolean;
  /** ITR-4 presumptive vs ITR-3 full-books business. */
  businessPresumptiveOnly: boolean;
  otherSources: boolean;
  /** ITR-1 allows only a single self-occupied house property. */
  singleHouseProperty: boolean;
}

export function incomeHeads(itr: ItrType | null | undefined): IncomeHeadVisibility {
  switch (itr) {
    case 'ITR1':
      return { salary: true, houseProperty: true, capitalGains: false, business: false, businessPresumptiveOnly: false, otherSources: true, singleHouseProperty: true };
    case 'ITR4':
      return { salary: true, houseProperty: true, capitalGains: false, business: true, businessPresumptiveOnly: true, otherSources: true, singleHouseProperty: true };
    case 'ITR2':
      return { salary: true, houseProperty: true, capitalGains: true, business: false, businessPresumptiveOnly: false, otherSources: true, singleHouseProperty: false };
    case 'ITR3':
      return { salary: true, houseProperty: true, capitalGains: true, business: true, businessPresumptiveOnly: false, otherSources: true, singleHouseProperty: false };
    default:
      // Unknown / not-yet-selected: show the broadest safe set (salary + others).
      return { salary: true, houseProperty: true, capitalGains: false, business: false, businessPresumptiveOnly: false, otherSources: true, singleHouseProperty: true };
  }
}
