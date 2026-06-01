// Maps a step slug to its body component.
import type { ComponentType } from 'react';
import type { WizardStepSlug } from '../steps';
import { PersonalStep } from './PersonalStep';
import { DocumentsStep } from './DocumentsStep';
import { IncomeStep } from './IncomeStep';
import { DeductionsStep } from './DeductionsStep';
import { RegimeStep } from './RegimeStep';
import { SummaryStep } from './SummaryStep';
import { PaymentStep } from './PaymentStep';
import { FileStep } from './FileStep';

export const STEP_COMPONENTS: Record<WizardStepSlug, ComponentType> = {
  personal: PersonalStep,
  documents: DocumentsStep,
  income: IncomeStep,
  deductions: DeductionsStep,
  regime: RegimeStep,
  summary: SummaryStep,
  payment: PaymentStep,
  file: FileStep,
};
