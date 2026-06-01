'use client';

// ---------------------------------------------------------------------------
// DocumentsTable — the document vault list. Each row shows the kind, file name,
// size, a status badge, and actions: Review (opens the extraction drawer when an
// extraction exists) and Download (streams the original via the API + RBAC).
// Data comes from the parent via TanStack Query; this component is presentational
// apart from the download mutation (a one-shot blob fetch → save).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Download, FileSearch, Loader2 } from 'lucide-react';
import { Table, THead, TBody, TR, TH, TD, Badge, Button } from '@/components/ui';
import { formatBytes, formatDateTime } from '@/lib/format';
import { ApiError } from '@/lib/api';
import { downloadDocument } from '../api';
import {
  canReviewExtraction,
  documentStatusTone,
  formatDocumentKind,
  formatDocumentStatus,
} from '../helpers';
import type { DocumentDto } from '../types';

export interface DocumentsTableProps {
  documents: DocumentDto[];
  onReview: (doc: DocumentDto) => void;
}

export function DocumentsTable({ documents, onReview }: DocumentsTableProps) {
  // Track which row is currently downloading so we can show a spinner.
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const download = useMutation({
    mutationFn: (doc: DocumentDto) => downloadDocument(doc.id),
    onMutate: (doc) => {
      setError(null);
      setDownloadingId(doc.id);
    },
    onSuccess: ({ blob, fileName }, doc) => {
      saveBlob(blob, fileName ?? doc.fileName);
    },
    onError: (err) => {
      setError(
        err instanceof ApiError
          ? (err.problem.detail ?? err.message)
          : 'Download failed. Please try again.',
      );
    },
    onSettled: () => setDownloadingId(null),
  });

  return (
    <div className="space-y-3">
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
      <Table>
        <THead>
          <TR>
            <TH>Document</TH>
            <TH className="hidden sm:table-cell">Type</TH>
            <TH>Status</TH>
            <TH className="hidden md:table-cell">Uploaded</TH>
            <TH className="text-right">Actions</TH>
          </TR>
        </THead>
        <TBody>
          {documents.map((doc) => {
            const reviewable = canReviewExtraction(doc);
            const isDownloading = downloadingId === doc.id;
            return (
              <TR key={doc.id}>
                <TD>
                  <div className="font-medium text-ink-900">{doc.fileName}</div>
                  <div className="text-xs text-ink-500">
                    <span className="sm:hidden">{formatDocumentKind(doc.kind)} · </span>
                    {formatBytes(doc.sizeBytes)}
                  </div>
                </TD>
                <TD className="hidden sm:table-cell">{formatDocumentKind(doc.kind)}</TD>
                <TD>
                  <Badge tone={documentStatusTone(doc.status)}>
                    {formatDocumentStatus(doc.status)}
                  </Badge>
                </TD>
                <TD className="hidden md:table-cell text-ink-500">
                  {formatDateTime(doc.createdAt)}
                </TD>
                <TD>
                  <div className="flex items-center justify-end gap-1.5">
                    {reviewable && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onReview(doc)}
                        aria-label={`Review extracted data for ${doc.fileName}`}
                      >
                        <FileSearch className="h-4 w-4" aria-hidden="true" />
                        <span className="hidden sm:inline">Review</span>
                      </Button>
                    )}
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => download.mutate(doc)}
                      disabled={isDownloading}
                      aria-label={`Download ${doc.fileName}`}
                    >
                      {isDownloading ? (
                        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                      ) : (
                        <Download className="h-4 w-4" aria-hidden="true" />
                      )}
                      <span className="hidden sm:inline">Download</span>
                    </Button>
                  </div>
                </TD>
              </TR>
            );
          })}
        </TBody>
      </Table>
    </div>
  );
}

/** Trigger a browser download for a fetched blob. */
function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  // Revoke on the next tick so the click has time to start the download.
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
