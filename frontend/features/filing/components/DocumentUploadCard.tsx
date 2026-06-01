'use client';

// ---------------------------------------------------------------------------
// DocumentUploadCard — upload a single document KIND (e.g. Form16, Form26AS) for
// the return using the two-step pre-signed flow:
//   1. POST /documents:initiate-upload  -> { documentId, uploadUrl, uploadHeaders }
//   2. PUT bytes to uploadUrl (dev loopback streams to IFileStorage)
//   3. POST /documents/{id}:complete    -> runs the (stubbed) extraction
// then we fetch the extraction and render <ExtractionReview/> for HITL approval.
// ---------------------------------------------------------------------------

import { useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, UploadCloud } from 'lucide-react';
import { Alert, Badge, Button, Card, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatBytes } from '@/lib/format';
import { cn } from '@/lib/utils';
import {
  completeUpload,
  filingKeys,
  getExtraction,
  initiateUpload,
  uploadBytes,
} from '../api';
import type { DocumentDto } from '../types';
import { ExtractionReview } from './ExtractionReview';

const MAX_BYTES = 10 * 1024 * 1024; // 10 MB (matches the UX copy)
const ACCEPT = 'application/pdf,image/png,image/jpeg';

export function DocumentUploadCard({
  returnId,
  kind,
  title,
  description,
  existing,
  onChanged,
}: {
  returnId: string;
  kind: string;
  title: string;
  description?: string;
  existing?: DocumentDto;
  onChanged?: () => void;
}) {
  const t = useTranslations('documents');
  const tc = useTranslations('common');
  const qc = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  // The active document id is either the freshly uploaded one or any existing one.
  const [uploadedId, setUploadedId] = useState<string | null>(existing?.id ?? null);
  const activeDocId = uploadedId ?? existing?.id ?? null;

  const uploadMutation = useMutation({
    mutationFn: async (file: File) => {
      const init = await initiateUpload({
        kind,
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        returnId,
      });
      await uploadBytes(init.uploadUrl, init.uploadHeaders, file);
      const doc = await completeUpload(init.documentId, {});
      return doc;
    },
    onSuccess: (doc) => {
      setUploadedId(doc.id);
      void qc.invalidateQueries({ queryKey: filingKeys.documents(returnId) });
      onChanged?.();
    },
  });

  // Once a document exists & reports an extraction, load it for review.
  const extractionQuery = useQuery({
    queryKey: activeDocId ? filingKeys.extraction(activeDocId) : ['extraction', 'none'],
    queryFn: () => getExtraction(activeDocId as string),
    enabled: !!activeDocId,
    retry: false,
    staleTime: 10_000,
  });

  function handleFile(file: File | undefined) {
    setLocalError(null);
    if (!file) return;
    if (file.size > MAX_BYTES) {
      setLocalError(t('tooLarge'));
      return;
    }
    uploadMutation.mutate(file);
  }

  const uploadError =
    uploadMutation.error instanceof ApiError
      ? (uploadMutation.error.problem.detail ?? uploadMutation.error.message)
      : uploadMutation.error
        ? tc('retry')
        : null;

  const uploaded = uploadMutation.data ?? existing;

  return (
    <Card className="p-5">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold text-ink-900">{title}</h3>
          {description && <p className="text-sm text-ink-500">{description}</p>}
        </div>
        {uploaded && <Badge tone="success">{t('uploaded')}</Badge>}
      </div>

      {!uploaded && (
        <>
          {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions */}
          <div
            role="button"
            tabIndex={0}
            onClick={() => inputRef.current?.click()}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                inputRef.current?.click();
              }
            }}
            onDragOver={(e) => {
              e.preventDefault();
              setDragOver(true);
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragOver(false);
              handleFile(e.dataTransfer.files?.[0]);
            }}
            className={cn(
              'flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-4 py-8 text-center transition-colors',
              dragOver ? 'border-brand-500 bg-brand-50' : 'border-ink-300 hover:border-brand-400 hover:bg-ink-50',
            )}
          >
            {uploadMutation.isPending ? (
              <Spinner label={t('uploading')} />
            ) : (
              <>
                <UploadCloud className="h-7 w-7 text-ink-400" aria-hidden="true" />
                <span className="text-sm font-medium text-ink-700">{t('dropzone')}</span>
                <span className="text-xs text-ink-400">{t('dropzoneHint')}</span>
              </>
            )}
            <input
              ref={inputRef}
              type="file"
              accept={ACCEPT}
              className="hidden"
              onChange={(e) => handleFile(e.target.files?.[0] ?? undefined)}
            />
          </div>
        </>
      )}

      {uploaded && (
        <div className="flex items-center gap-3 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5">
          <FileText className="h-5 w-5 text-brand-600" aria-hidden="true" />
          <div className="min-w-0 flex-1">
            <div className="truncate text-sm font-medium text-ink-800">{uploaded.fileName}</div>
            <div className="text-xs text-ink-400">{formatBytes(uploaded.sizeBytes)}</div>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setUploadedId(null);
              uploadMutation.reset();
              inputRef.current?.click();
            }}
          >
            {t('replace')}
          </Button>
        </div>
      )}

      {(localError || uploadError) && (
        <Alert variant="error" className="mt-3">
          {localError ?? uploadError}
        </Alert>
      )}

      {/* Extraction review */}
      {activeDocId && (
        <div className="mt-4">
          {extractionQuery.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-ink-500">
              <Spinner /> {t('extracting')}
            </div>
          ) : extractionQuery.data ? (
            <ExtractionReview
              extraction={extractionQuery.data}
              onApproved={() => {
                void qc.invalidateQueries({ queryKey: filingKeys.detail(returnId) });
                onChanged?.();
              }}
            />
          ) : null}
        </div>
      )}
    </Card>
  );
}
