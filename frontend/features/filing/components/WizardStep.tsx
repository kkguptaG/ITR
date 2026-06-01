'use client';

// ---------------------------------------------------------------------------
// WizardStep / WizardFooter — consistent step chrome:
//   <WizardStep title description>  …step body…  <WizardFooter>…CTA…</WizardFooter>
// The footer is sticky at the bottom on mobile so the primary action is always
// reachable. The Back button is wired to the wizard's goPrev.
// ---------------------------------------------------------------------------

import { type ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronLeft } from 'lucide-react';
import { Button } from '@/components/ui';
import { useWizard } from '../WizardContext';

export function WizardStep({
  title,
  description,
  children,
}: {
  title: ReactNode;
  description?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="animate-fade-in space-y-5">
      <header className="space-y-1">
        <h2 className="text-xl font-semibold text-ink-900">{title}</h2>
        {description && <p className="text-sm text-ink-500">{description}</p>}
      </header>
      {children}
    </div>
  );
}

/**
 * Sticky action bar. Pass the primary CTA via `primary`; `onBack` defaults to the
 * wizard's goPrev. `secondary` renders to the left of the primary (e.g. "Skip").
 */
export function WizardFooter({
  primary,
  secondary,
  backLabel,
  hideBack,
}: {
  primary: ReactNode;
  secondary?: ReactNode;
  backLabel?: string;
  hideBack?: boolean;
}) {
  const t = useTranslations('common');
  const { goPrev } = useWizard();
  return (
    <div className="fixed inset-x-0 bottom-0 z-20 border-t border-ink-200 bg-white/95 backdrop-blur sm:static sm:mt-8 sm:border-0 sm:bg-transparent sm:p-0">
      <div className="mx-auto flex max-w-3xl items-center justify-between gap-3 px-4 py-3 sm:px-0">
        {hideBack ? (
          <span />
        ) : (
          <Button type="button" variant="ghost" onClick={goPrev}>
            <ChevronLeft className="h-4 w-4" aria-hidden="true" />
            {backLabel ?? t('back')}
          </Button>
        )}
        <div className="flex items-center gap-2">
          {secondary}
          {primary}
        </div>
      </div>
    </div>
  );
}
