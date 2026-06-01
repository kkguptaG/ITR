'use client';

// ---------------------------------------------------------------------------
// /documents — the secure document vault.
//   • FileDropzone: two-step pre-signed upload (initiate → PUT → complete).
//   • DocumentsTable: kind + status badges, Review (extraction drawer), Download.
//   • ExtractionReviewDrawer: HITL review of parsed fields → extraction:approve.
//   • Filters by kind/status; paged via PagedResult.
// All data flows through TanStack Query against the real /documents endpoints.
// ---------------------------------------------------------------------------

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslations } from 'next-intl';
import { FolderOpen } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  EmptyState,
  Spinner,
  Alert,
  Select,
  Button,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { FileDropzone } from '@/features/documents/components/FileDropzone';
import { DocumentsTable } from '@/features/documents/components/DocumentsTable';
import { ExtractionReviewDrawer } from '@/features/documents/components/ExtractionReviewDrawer';
import { documentsKeys, listDocuments, type ListDocumentsParams } from '@/features/documents/api';
import {
  UPLOAD_KIND_OPTIONS,
  formatDocumentStatus,
} from '@/features/documents/helpers';
import type { DocumentDto, DocumentKind, DocumentStatus } from '@/features/documents/types';

const PAGE_SIZE = 20;

const STATUS_FILTERS: DocumentStatus[] = [
  'Uploaded',
  'Extracting',
  'Extracted',
  'NeedsReview',
  'Verified',
  'Failed',
];

export default function DocumentsPage() {
  const t = useTranslations('documents');
  const tc = useTranslations('common');

  const [page, setPage] = useState(1);
  const [kind, setKind] = useState<DocumentKind | ''>('');
  const [status, setStatus] = useState<DocumentStatus | ''>('');
  const [reviewDoc, setReviewDoc] = useState<DocumentDto | null>(null);

  const params: ListDocumentsParams = useMemo(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      kind: kind || undefined,
      status: status || undefined,
    }),
    [page, kind, status],
  );

  const documentsQuery = useQuery({
    queryKey: documentsKeys.list(params),
    queryFn: () => listDocuments(params),
    // Keep the previous page visible while the next loads (smooth pagination).
    placeholderData: (prev) => prev,
  });

  const data = documentsQuery.data;
  const documents = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const hasFilters = !!kind || !!status;

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-ink-900">{t('title')}</h1>
          <p className="mt-1 text-sm text-ink-500">
            Upload your Form 16, AIS, 26AS and statements — we extract and reconcile the figures
            for you.
          </p>
        </div>
      </header>

      {/* Upload card */}
      <Card>
        <CardHeader>
          <CardTitle>Upload documents</CardTitle>
          <CardDescription>{t('dropzoneHint')}</CardDescription>
        </CardHeader>
        <CardContent>
          <FileDropzone />
        </CardContent>
      </Card>

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1.5">
          <label htmlFor="filter-kind" className="text-xs font-medium uppercase tracking-wide text-ink-500">
            Type
          </label>
          <Select
            id="filter-kind"
            value={kind}
            onChange={(e) => {
              setKind(e.target.value as DocumentKind | '');
              setPage(1);
            }}
            className="w-48"
          >
            <option value="">All types</option>
            {UPLOAD_KIND_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </div>
        <div className="space-y-1.5">
          <label htmlFor="filter-status" className="text-xs font-medium uppercase tracking-wide text-ink-500">
            Status
          </label>
          <Select
            id="filter-status"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value as DocumentStatus | '');
              setPage(1);
            }}
            className="w-48"
          >
            <option value="">All statuses</option>
            {STATUS_FILTERS.map((s) => (
              <option key={s} value={s}>
                {formatDocumentStatus(s)}
              </option>
            ))}
          </Select>
        </div>
        {hasFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setKind('');
              setStatus('');
              setPage(1);
            }}
          >
            Clear filters
          </Button>
        )}
      </div>

      {/* List */}
      {documentsQuery.isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner label={tc('loading')} />
        </div>
      ) : documentsQuery.isError ? (
        <Alert variant="error" title="Couldn’t load your documents">
          {documentsQuery.error instanceof ApiError
            ? (documentsQuery.error.problem.detail ?? documentsQuery.error.message)
            : 'Please try again.'}
        </Alert>
      ) : documents.length === 0 ? (
        <EmptyState
          icon={FolderOpen}
          title={hasFilters ? 'No matching documents' : t('emptyTitle')}
          description={hasFilters ? 'Try clearing the filters above.' : t('emptyBody')}
        />
      ) : (
        <>
          <DocumentsTable documents={documents} onReview={setReviewDoc} />

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm text-ink-500">
              <span>
                Page {page} of {totalPages} · {total} document{total === 1 ? '' : 's'}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  {tc('back')}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  {tc('next')}
                </Button>
              </div>
            </div>
          )}
        </>
      )}

      <ExtractionReviewDrawer
        document={reviewDoc}
        open={!!reviewDoc}
        onClose={() => setReviewDoc(null)}
      />
    </div>
  );
}
