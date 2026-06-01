'use client';

// ---------------------------------------------------------------------------
// ExtractionReview — shows a document's extracted fields (key/value/confidence)
// for human review. Low-confidence fields are flagged. The reviewer can edit any
// value inline before approving; on approve the verified fields are mapped onto
// the return (income sources / deductions) via POST /documents/{id}/extraction:approve.
// Tolerates the stubbed extractor: missing/empty fields render gracefully.
// ---------------------------------------------------------------------------

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation } from '@tanstack/react-query';
import { CheckCircle2, ShieldAlert } from 'lucide-react';
import { Alert, Badge, Button, Input } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { cn } from '@/lib/utils';
import { approveExtraction } from '../api';
import type { ApproveExtractionResponse, ExtractionDto } from '../types';

const LOW_CONFIDENCE = 0.7;

export function ExtractionReview({
  extraction,
  onApproved,
}: {
  extraction: ExtractionDto;
  onApproved?: (result: ApproveExtractionResponse) => void;
}) {
  const t = useTranslations('documents');
  const tc = useTranslations('common');

  // Local editable copy of the field values (HITL corrections).
  const [edited, setEdited] = useState<Record<string, string>>({});
  const value = (key: string, original: string | null) =>
    edited[key] ?? original ?? '';

  const overrides = useMemo(() => {
    const diff: Record<string, string> = {};
    for (const f of extraction.fields) {
      const v = edited[f.key];
      if (v !== undefined && v !== (f.value ?? '')) diff[f.key] = v;
    }
    return diff;
  }, [edited, extraction.fields]);

  const approveMutation = useMutation({
    mutationFn: () =>
      approveExtraction(extraction.documentId, {
        mapToReturn: true,
        fieldOverrides: Object.keys(overrides).length ? overrides : null,
      }),
    onSuccess: (res) => onApproved?.(res),
  });

  const alreadyApproved = !!extraction.reviewedAt;

  const errorMessage =
    approveMutation.error instanceof ApiError
      ? (approveMutation.error.problem.detail ?? approveMutation.error.message)
      : approveMutation.error
        ? tc('retry')
        : null;

  if (extraction.fields.length === 0) {
    return (
      <Alert variant="info">{t('noFieldsExtracted')}</Alert>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm text-ink-600">{t('reviewExtractedFields')}</p>
        {typeof extraction.confidenceScore === 'number' && (
          <Badge tone={extraction.confidenceScore >= LOW_CONFIDENCE ? 'success' : 'warning'}>
            {Math.round(extraction.confidenceScore * 100)}% {t('confidence')}
          </Badge>
        )}
      </div>

      <div className="overflow-hidden rounded-xl border border-ink-200">
        <table className="w-full text-sm">
          <tbody className="divide-y divide-ink-100">
            {extraction.fields.map((f) => {
              const low = typeof f.confidence === 'number' && f.confidence < LOW_CONFIDENCE;
              return (
                <tr key={f.key} className={cn(low && 'bg-payable-50/50')}>
                  <td className="w-2/5 px-3 py-2 align-middle">
                    <span className="font-medium text-ink-700">{humanizeKey(f.key)}</span>
                    {low && (
                      <span className="ml-2 inline-flex items-center gap-1 text-xs text-payable-700">
                        <ShieldAlert className="h-3 w-3" aria-hidden="true" />
                        {t('lowConfidence')}
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-2">
                    <Input
                      value={value(f.key, f.value)}
                      onChange={(e) => setEdited((s) => ({ ...s, [f.key]: e.target.value }))}
                      disabled={alreadyApproved}
                      invalid={low && !edited[f.key]}
                      className="h-9"
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {errorMessage && <Alert variant="error">{errorMessage}</Alert>}

      {approveMutation.isSuccess ? (
        <Alert variant="success" title={t('extractionApproved')}>
          {t('mappedSummary', {
            income: approveMutation.data.incomeSourcesUpserted,
            deductions: approveMutation.data.deductionsUpserted,
          })}
        </Alert>
      ) : alreadyApproved ? (
        <div className="inline-flex items-center gap-1.5 text-sm text-money-700">
          <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
          {t('extractionApproved')}
        </div>
      ) : (
        <Button onClick={() => approveMutation.mutate()} loading={approveMutation.isPending}>
          {t('approveAndApply')}
        </Button>
      )}
    </div>
  );
}

/** Turn a snake/camel field key into a human label (e.g. "grossSalary" -> "Gross salary"). */
function humanizeKey(key: string): string {
  const spaced = key
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
