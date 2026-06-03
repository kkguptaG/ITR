'use client';

// ---------------------------------------------------------------------------
// Step 2 — Documents. Upload Form 16 + Form 26AS (AIS optional) via the
// two-step pre-signed flow; the stubbed extractor returns parsed fields which
// the user reviews/approves, mapping them onto the return. Documents are
// optional to proceed (a user can key figures manually on the Income step).
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { Button, Alert } from '@/components/ui';
import { filingKeys, listDocuments } from '../api';
import type { DocumentDto } from '../types';
import { useWizard } from '../WizardContext';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { DocumentUploadCard } from '../components/DocumentUploadCard';

const KINDS: { kind: string; titleKey: string; descKey: string }[] = [
  { kind: 'Form16', titleKey: 'form16', descKey: 'form16Desc' },
  { kind: 'Form26AS', titleKey: 'form26as', descKey: 'form26asDesc' },
  { kind: 'AIS', titleKey: 'ais', descKey: 'aisDesc' },
];

export function DocumentsStep() {
  const t = useTranslations('wizard');
  const td = useTranslations('documents');
  const tc = useTranslations('common');
  const { returnId, goNext } = useWizard();

  const docsQuery = useQuery({
    queryKey: filingKeys.documents(returnId),
    queryFn: () => listDocuments(returnId),
    staleTime: 5_000,
  });

  const byKind = (kind: string): DocumentDto | undefined =>
    docsQuery.data?.items.find((d) => d.kind === kind);

  return (
    <>
      <WizardStep title={t('documentsTitle')} description={t('documentsSubtitle')}>
        <Alert variant="info">{td('uploadHelp')}</Alert>

        <div className="space-y-4">
          {KINDS.map(({ kind, titleKey, descKey }) => (
            <DocumentUploadCard
              key={kind}
              returnId={returnId}
              kind={kind}
              title={td(titleKey)}
              description={td(descKey)}
              existing={byKind(kind)}
              onChanged={() => void docsQuery.refetch()}
            />
          ))}
        </div>

        <Alert variant="info">
          Have bank statements? Import them on the{' '}
          <Link
            href={`/accounting/vouchers?returnId=${returnId}`}
            className="font-semibold underline underline-offset-2 hover:opacity-75"
          >
            Bank Statement page
          </Link>{' '}
          — interest, dividends and income you approve can be pushed directly to this return.
        </Alert>
      </WizardStep>

      <WizardFooter
        primary={
          <Button type="button" onClick={goNext}>
            {tc('continue')}
          </Button>
        }
      />
    </>
  );
}
