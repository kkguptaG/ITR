'use client';

// ---------------------------------------------------------------------------
// features/filing/WizardContext.tsx
// Shares the loaded return + current step + navigation helpers down the wizard
// tree so each step body stays thin. Also exposes a tiny autosave-status signal
// the layout renders ("Saving…" / "Saved") and a goNext/goPrev that performs
// optimistic client-side navigation.
// ---------------------------------------------------------------------------

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { useRouter } from 'next/navigation';
import type { ReturnDetailDto } from './types';
import {
  WIZARD_STEPS,
  nextStep,
  prevStep,
  stepHref,
  type WizardStepSlug,
} from './steps';
import { isReturnLocked } from './useReturn';

export type SaveState = 'idle' | 'saving' | 'saved' | 'error';

interface WizardContextValue {
  returnId: string;
  step: WizardStepSlug;
  detail: ReturnDetailDto;
  locked: boolean;
  saveState: SaveState;
  setSaveState: (s: SaveState) => void;
  goNext: () => void;
  goPrev: () => void;
  goToStep: (slug: WizardStepSlug) => void;
}

const WizardContext = createContext<WizardContextValue | null>(null);

export function WizardProvider({
  returnId,
  step,
  detail,
  children,
}: {
  returnId: string;
  step: WizardStepSlug;
  detail: ReturnDetailDto;
  children: ReactNode;
}) {
  const router = useRouter();
  const [saveState, setSaveState] = useState<SaveState>('idle');

  const goToStep = useCallback(
    (slug: WizardStepSlug) => router.push(stepHref(returnId, slug)),
    [router, returnId],
  );

  const goNext = useCallback(() => {
    const n = nextStep(step);
    if (n) router.push(stepHref(returnId, n));
  }, [router, returnId, step]);

  const goPrev = useCallback(() => {
    const p = prevStep(step);
    if (p) router.push(stepHref(returnId, p));
    else router.push(`/returns/${returnId}`);
  }, [router, returnId, step]);

  const value = useMemo<WizardContextValue>(
    () => ({
      returnId,
      step,
      detail,
      locked: isReturnLocked(detail),
      saveState,
      setSaveState,
      goNext,
      goPrev,
      goToStep,
    }),
    [returnId, step, detail, saveState, goNext, goPrev, goToStep],
  );

  return <WizardContext.Provider value={value}>{children}</WizardContext.Provider>;
}

export function useWizard(): WizardContextValue {
  const ctx = useContext(WizardContext);
  if (!ctx) throw new Error('useWizard must be used within <WizardProvider>.');
  return ctx;
}

export { WIZARD_STEPS };
