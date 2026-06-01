'use client';

// ---------------------------------------------------------------------------
// ExtractionReviewDrawer — the HITL (human-in-the-loop) review panel.
//   • GET /documents/{id}/extraction → render parsed fields with per-field
//     confidence (Ch.5 §5.2.4: money fields < 0.92 need review).
//   • Reviewer may correct any value inline; edits become `fieldOverrides`.
//   • Approve → POST /documents/{id}/extraction:approve (mapToReturn=true when
//     the doc is linked to a return), then invalidates the list + detail caches.
// Confidence is shown as a coloured chip so the eye lands on weak fields first.
// ---------------------------------------------------------------------------

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, FileSearch, Info } from 'lucide-react';
import { Button, Alert, Spinner, Badge, Input } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDateTime } from '@/lib/format';
import { Drawer } from './Drawer';
import {
  approveExtraction,
  documentsKeys,
  getExtraction,
} from '../api';
import {
  CONFIDENCE_THRESHOLD,
  formatDocumentKind,
  humanizeFieldKey,
  isMoneyField,
} from '../helpers';
import type { DocumentDto, ExtractedFieldDto } from '../types';

export interface ExtractionReviewDrawerProps {
  /** The document under review; null closes the drawer. */
  document: DocumentDto | null;
  open: boolean;
  onClose: () => void;
}

export function ExtractionReviewDrawer({ document, open, onClose }: ExtractionReviewDrawerProps) {
  const queryClient = useQueryClient();
  const documentId = document?.id ?? '';

  const extractionQuery = useQuery({
    queryKey: documentsKeys.extraction(documentId),
    queryFn: () => getExtraction(documentId),
    enabled: open && !!documentId,
  });

  // Local edits keyed by field key (HITL corrections → fieldOverrides on approve).
  const [edits, setEdits] = useState<Record<string, string>>({});

  // Reset edits whenever a different document opens.
  useEffect(() => {
    setEdits({});
  }, [documentId, open]);

  const approveMutation = useMutation({
    mutationFn: () => {
      const fieldOverrides = Object.keys(edits).length > 0 ? edits : null;
      return approveExtraction(documentId, {
        mapToReturn: !!document?.returnId,
        fieldOverrides,
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentsKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: documentsKeys.extraction(documentId) });
      void queryClient.invalidateQueries({ queryKey: documentsKeys.detail(documentId) });
    },
  });

  const extraction = extractionQuery.data;
  const alreadyApproved = !!extraction?.reviewedAt;
  const approved = approveMutation.isSuccess || alreadyApproved;
  const mappedResult = approveMutation.data;

  const sortedFields = useMemo(() => {
    if (!extraction) return [];
    // Surface the weakest (lowest-confidence) fields first so review is efficient.
    return [...extraction.fields].sort((a, b) => (a.confidence ?? 1) - (b.confidence ?? 1));
  }, [extraction]);

  const approveError =
    approveMutation.error instanceof ApiError
      ? (approveMutation.error.problem.detail ?? approveMutation.error.message)
      : approveMutation.error
        ? 'Could not approve. Please try again.'
        : null;

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Review extracted data"
      description={document ? formatDocumentKind(document.kind) + ' · ' + document.fileName : undefined}
      footer={
        document && extraction ? (
          <>
            <Button variant="ghost" onClick={onClose} disabled={approveMutation.isPending}>
              Close
            </Button>
            <Button
              onClick={() => approveMutation.mutate()}
              loading={approveMutation.isPending}
              disabled={approved}
            >
              {approved ? 'Approved' : 'Approve & use'}
            </Button>
          </>
        ) : undefined
      }
    >
      {!document ? null : extractionQuery.isLoading ? (
        <div className="flex justify-center py-10">
          <Spinner label="Loading extraction…" />
        </div>
      ) : extractionQuery.isError ? (
        <Alert variant="error" title="Couldn’t load extraction">
          {extractionQuery.error instanceof ApiError
            ? (extractionQuery.error.problem.detail ?? extractionQuery.error.message)
            : 'No extraction is available for this document yet.'}
        </Alert>
      ) : !extraction ? null : (
        <div className="space-y-4">
          {/* Summary header: doc class + aggregate confidence + review banner. */}
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Badge tone="neutral">{extraction.docClass}</Badge>
            <ConfidenceChip confidence={extraction.confidenceScore} aggregate />
            {extraction.needsReview && !approved && (
              <Badge tone="warning">Needs review</Badge>
            )}
            {approved && <Badge tone="success">Verified</Badge>}
          </div>

          {approved ? (
            <Alert variant="success" title="Extraction approved">
              {mappedResult
                ? `Mapped ${mappedResult.incomeSourcesUpserted} income source(s) and ${mappedResult.deductionsUpserted} deduction(s) onto your return.`
                : extraction.reviewedAt
                  ? `Reviewed ${formatDateTime(extraction.reviewedAt)}.`
                  : 'These values have been verified.'}
            </Alert>
          ) : extraction.needsReview ? (
            <Alert variant="warning" title="Please confirm the highlighted values">
              One or more money fields scored below {Math.round(CONFIDENCE_THRESHOLD * 100)}%
              confidence. Correct anything that looks off, then approve.
            </Alert>
          ) : (
            <Alert variant="info" title="High-confidence extraction">
              These values were read with high confidence. Review and approve to map them onto
              your return.
            </Alert>
          )}

          {sortedFields.length === 0 ? (
            <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-ink-300 py-8 text-center text-sm text-ink-500">
              <FileSearch className="h-6 w-6 text-ink-400" aria-hidden="true" />
              No fields were extracted from this document.
            </div>
          ) : (
            <ul className="space-y-3">
              {sortedFields.map((field) => (
                <FieldRow
                  key={field.key}
                  field={field}
                  value={edits[field.key] ?? field.value ?? ''}
                  readOnly={approved}
                  onChange={(v) => setEdits((prev) => ({ ...prev, [field.key]: v }))}
                />
              ))}
            </ul>
          )}

          {!approved && (
            <p className="flex items-start gap-2 text-xs text-ink-500">
              <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              Edited values are saved as corrections and used in your computation. The original
              file is kept unchanged as evidence.
            </p>
          )}

          {approveError && <Alert variant="error">{approveError}</Alert>}
        </div>
      )}
    </Drawer>
  );
}

// ----------------------------------------------------------------- field row

function FieldRow({
  field,
  value,
  readOnly,
  onChange,
}: {
  field: ExtractedFieldDto;
  value: string;
  readOnly: boolean;
  onChange: (value: string) => void;
}) {
  const low = (field.confidence ?? 1) < CONFIDENCE_THRESHOLD;
  const money = isMoneyField(field.key);
  return (
    <li className="space-y-1.5">
      <div className="flex items-center justify-between gap-2">
        <label
          htmlFor={`field-${field.key}`}
          className="text-sm font-medium text-ink-800"
        >
          {humanizeFieldKey(field.key)}
          {money && <span className="ml-1 text-xs font-normal text-ink-400">(₹)</span>}
        </label>
        <ConfidenceChip confidence={field.confidence} />
      </div>
      <Input
        id={`field-${field.key}`}
        value={value}
        readOnly={readOnly}
        invalid={low && !readOnly}
        onChange={(e) => onChange(e.target.value)}
        className={readOnly ? 'bg-ink-50 text-ink-600' : undefined}
      />
    </li>
  );
}

// ----------------------------------------------------------------- confidence chip

function ConfidenceChip({
  confidence,
  aggregate,
}: {
  confidence: number | null | undefined;
  aggregate?: boolean;
}) {
  if (confidence === null || confidence === undefined) {
    return <Badge tone="neutral">—</Badge>;
  }
  const pct = Math.round(confidence * 100);
  const tone = confidence >= CONFIDENCE_THRESHOLD ? 'success' : confidence >= 0.8 ? 'warning' : 'danger';
  return (
    <Badge tone={tone}>
      {aggregate ? 'Overall ' : ''}
      {pct}%
    </Badge>
  );
}
