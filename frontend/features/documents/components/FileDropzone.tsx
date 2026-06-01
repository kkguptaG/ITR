'use client';

// ---------------------------------------------------------------------------
// FileDropzone — drag-and-drop / browse upload for the Documents vault.
//   Each picked file runs the two-step pre-signed upload (Decision Log D-2):
//     1. POST /documents:initiate-upload  → { uploadUrl, method, headers }
//     2. PUT  bytes to the pre-signed URL (off-API in prod; loopback in dev)
//     3. POST /documents/{id}:complete    → extraction runs synchronously (stub)
//   A document `kind` is chosen for the whole batch before dropping. On each
//   successful complete we invalidate the documents list so the row appears.
// ---------------------------------------------------------------------------

import { useCallback, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { UploadCloud, FileText, CheckCircle2, AlertCircle, Loader2, X } from 'lucide-react';
import { Select, Alert } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatBytes } from '@/lib/format';
import { ApiError } from '@/lib/api';
import { completeUpload, documentsKeys, initiateUpload, uploadBytes } from '../api';
import { UPLOAD_KIND_OPTIONS } from '../helpers';
import type { DocumentKind } from '../types';

/** Client-side guardrails mirroring the server allow-list (Ch.5 §5.1.1). */
const MAX_SIZE_BYTES = 50 * 1024 * 1024; // 50 MB
const ACCEPT =
  'application/pdf,image/jpeg,image/png,image/heic,text/csv,application/json,.pdf,.jpg,.jpeg,.png,.heic,.csv,.json,.xls,.xlsx';
const ALLOWED_PREFIXES = ['application/pdf', 'image/', 'text/csv', 'application/json', 'application/vnd.'];

type UploadPhase = 'queued' | 'uploading' | 'completing' | 'done' | 'error';

interface UploadItem {
  id: string;
  file: File;
  phase: UploadPhase;
  error?: string;
}

export interface FileDropzoneProps {
  /** Optional return to link the uploads to (attaches the document to a return). */
  returnId?: string;
  /** Called after each file finishes (success or failure) — handy for toasts/refresh. */
  onUploaded?: () => void;
  className?: string;
}

let uploadSeq = 0;

export function FileDropzone({ returnId, onUploaded, className }: FileDropzoneProps) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const [kind, setKind] = useState<DocumentKind>('Form16');
  const [dragOver, setDragOver] = useState(false);
  const [items, setItems] = useState<UploadItem[]>([]);

  const patchItem = useCallback((id: string, patch: Partial<UploadItem>) => {
    setItems((prev) => prev.map((it) => (it.id === id ? { ...it, ...patch } : it)));
  }, []);

  const runUpload = useCallback(
    async (item: UploadItem, docKind: DocumentKind) => {
      try {
        patchItem(item.id, { phase: 'uploading' });
        const init = await initiateUpload({
          kind: docKind,
          fileName: item.file.name,
          contentType: item.file.type || 'application/octet-stream',
          returnId: returnId ?? null,
        });
        await uploadBytes(init, item.file);

        patchItem(item.id, { phase: 'completing' });
        await completeUpload(init.documentId, {});

        patchItem(item.id, { phase: 'done' });
        void queryClient.invalidateQueries({ queryKey: documentsKeys.lists() });
      } catch (err) {
        const message =
          err instanceof ApiError
            ? (err.problem.detail ?? err.message)
            : 'Upload failed. Please try again.';
        patchItem(item.id, { phase: 'error', error: message });
      } finally {
        onUploaded?.();
      }
    },
    [patchItem, queryClient, returnId, onUploaded],
  );

  const addFiles = useCallback(
    (fileList: FileList | File[]) => {
      const files = Array.from(fileList);
      if (files.length === 0) return;

      const accepted: UploadItem[] = [];
      const rejected: string[] = [];

      for (const file of files) {
        if (file.size > MAX_SIZE_BYTES) {
          rejected.push(`${file.name} exceeds the 50 MB limit`);
          continue;
        }
        const type = file.type || '';
        const looksAllowed =
          type === '' || ALLOWED_PREFIXES.some((p) => type.startsWith(p));
        if (!looksAllowed) {
          rejected.push(`${file.name} is not a supported file type`);
          continue;
        }
        uploadSeq += 1;
        accepted.push({ id: `u-${uploadSeq}`, file, phase: 'queued' });
      }

      if (rejected.length > 0) {
        // Surface rejects as transient error rows so the user sees why.
        const errorRows: UploadItem[] = rejected.map((message): UploadItem => {
          uploadSeq += 1;
          return {
            id: `r-${uploadSeq}`,
            file: new File([], message),
            phase: 'error',
            error: message,
          };
        });
        setItems((prev) => [...errorRows, ...accepted, ...prev]);
      } else {
        setItems((prev) => [...accepted, ...prev]);
      }

      accepted.forEach((item) => void runUpload(item, kind));
    },
    [kind, runUpload],
  );

  const onDrop = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      setDragOver(false);
      if (e.dataTransfer.files?.length) addFiles(e.dataTransfer.files);
    },
    [addFiles],
  );

  const onInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (e.target.files?.length) addFiles(e.target.files);
      // Reset so picking the same file again re-triggers change.
      e.target.value = '';
    },
    [addFiles],
  );

  const dismissItem = (id: string) => setItems((prev) => prev.filter((it) => it.id !== id));

  return (
    <div className={cn('space-y-4', className)}>
      <div className="grid gap-3 sm:grid-cols-[minmax(0,16rem)_1fr] sm:items-end">
        <div className="space-y-1.5">
          <label htmlFor="upload-kind" className="text-sm font-medium text-ink-700">
            Document type
          </label>
          <Select
            id="upload-kind"
            value={kind}
            onChange={(e) => setKind(e.target.value as DocumentKind)}
            options={UPLOAD_KIND_OPTIONS}
          />
        </div>
      </div>

      <div
        role="button"
        tabIndex={0}
        aria-label="Upload documents"
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
        onDrop={onDrop}
        className={cn(
          'flex cursor-pointer flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed px-6 py-10 text-center transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2',
          dragOver
            ? 'border-brand-500 bg-brand-50'
            : 'border-ink-300 bg-white hover:border-brand-400 hover:bg-ink-50',
        )}
      >
        <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
          <UploadCloud className="h-6 w-6" aria-hidden="true" />
        </div>
        <p className="text-sm font-medium text-ink-900">Drag &amp; drop or browse</p>
        <p className="max-w-xs text-xs text-ink-500">
          PDF, JPG, PNG, CSV or JSON · max 50 MB · encrypted &amp; stored in India
        </p>
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={ACCEPT}
          className="hidden"
          onChange={onInputChange}
        />
      </div>

      {items.length > 0 && (
        <ul className="space-y-2" aria-label="Upload progress">
          {items.map((item) => (
            <li
              key={item.id}
              className="flex items-center gap-3 rounded-xl border border-ink-200 bg-white px-3.5 py-2.5"
            >
              <UploadRowIcon phase={item.phase} />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-ink-900">
                  {item.phase === 'error' && item.file.size === 0
                    ? (item.error ?? 'Upload failed')
                    : item.file.name}
                </p>
                <p className="text-xs text-ink-500">
                  <UploadRowStatus item={item} />
                </p>
              </div>
              {(item.phase === 'done' || item.phase === 'error') && (
                <button
                  type="button"
                  onClick={() => dismissItem(item.id)}
                  aria-label="Dismiss"
                  className="rounded-lg p-1 text-ink-400 hover:bg-ink-100 hover:text-ink-700"
                >
                  <X className="h-4 w-4" aria-hidden="true" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {items.some((it) => it.phase === 'error') && (
        <Alert variant="warning">
          Some files couldn’t be uploaded. Check the file type and size, then try again.
        </Alert>
      )}
    </div>
  );
}

function UploadRowIcon({ phase }: { phase: UploadPhase }) {
  if (phase === 'done') {
    return <CheckCircle2 className="h-5 w-5 shrink-0 text-money-600" aria-hidden="true" />;
  }
  if (phase === 'error') {
    return <AlertCircle className="h-5 w-5 shrink-0 text-red-600" aria-hidden="true" />;
  }
  if (phase === 'uploading' || phase === 'completing') {
    return <Loader2 className="h-5 w-5 shrink-0 animate-spin text-brand-600" aria-hidden="true" />;
  }
  return <FileText className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />;
}

function UploadRowStatus({ item }: { item: UploadItem }) {
  switch (item.phase) {
    case 'queued':
      return <>Queued · {formatBytes(item.file.size)}</>;
    case 'uploading':
      return <>Uploading… · {formatBytes(item.file.size)}</>;
    case 'completing':
      return <>Extracting fields…</>;
    case 'done':
      return <>Uploaded · extraction ready</>;
    case 'error':
      return <span className="text-red-600">{item.error ?? 'Failed'}</span>;
    default:
      return null;
  }
}
