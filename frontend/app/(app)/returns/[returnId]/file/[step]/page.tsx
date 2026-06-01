// ---------------------------------------------------------------------------
// /returns/[returnId]/file/[step] — the guided ITR filing wizard.
// Thin server entry: extracts the route params and hands off to the client
// WizardRunner (which loads the return + renders the shared WizardLayout + step).
// ---------------------------------------------------------------------------

import { WizardRunner } from '@/features/filing/WizardRunner';

export default function FilingWizardPage({
  params,
}: {
  params: { returnId: string; step: string };
}) {
  return <WizardRunner returnId={params.returnId} step={params.step} />;
}
