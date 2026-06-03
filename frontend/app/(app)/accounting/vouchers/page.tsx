'use client';

import { Suspense, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { FileSpreadsheet } from 'lucide-react';
import {
  Alert,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  EmptyState,
  Spinner,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import {
  accountingKeys,
  BankStatementUpload,
  ImportReviewDrawer,
  ImportsTable,
  listImports,
} from '@/features/accounting';

const PAGE_SIZE = 10;

function VouchersContent() {
  const searchParams = useSearchParams();
  const returnId = searchParams.get('returnId');   // optional — passed when navigating from a return
  const [page, setPage] = useState(1);
  const [reviewId, setReviewId] = useState<string | null>(null);

  const params = { page, pageSize: PAGE_SIZE };
  const importsQuery = useQuery({
    queryKey: accountingKeys.imports(params),
    queryFn: () => listImports(params),
    placeholderData: (prev) => prev,
  });

  const data = importsQuery.data;
  const imports = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-ink-900">Bank statement → vouchers</h1>
        <p className="mt-1 text-sm text-ink-500">
          Import a bank statement (PDF, Excel or CSV). Each transaction is matched to the best-fit
          ledger; where none exists, a new account head is proposed with an{' '}
          <strong>(E)</strong> mark so you can trace and edit what was created. Review, then post
          double-entry vouchers into your books.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Import a statement</CardTitle>
          <CardDescription>
            We never change your original file — only the entries you approve are posted.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <BankStatementUpload onUploaded={(detail) => setReviewId(detail.import.id)} />
        </CardContent>
      </Card>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-ink-500">Recent imports</h2>

        {importsQuery.isLoading ? (
          <Spinner label="Loading imports…" />
        ) : importsQuery.isError ? (
          <Alert variant="error" title="Couldn't load imports">
            {importsQuery.error instanceof ApiError
              ? (importsQuery.error.problem.detail ?? importsQuery.error.message)
              : 'Please try again.'}
          </Alert>
        ) : imports.length === 0 ? (
          <EmptyState
            icon={FileSpreadsheet}
            title="No statements imported yet"
            description="Upload a bank statement above to auto-create vouchers and ledgers."
          />
        ) : (
          <>
            <ImportsTable imports={imports} onReview={setReviewId} />
            {totalPages > 1 && (
              <div className="flex items-center justify-between text-sm">
                <span className="text-ink-500">
                  Page {page} of {totalPages} · {total} import{total === 1 ? '' : 's'}
                </span>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    Back
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </section>

      <ImportReviewDrawer
        importId={reviewId}
        open={!!reviewId}
        onClose={() => setReviewId(null)}
        returnId={returnId}
      />
    </div>
  );
}

export default function VouchersPage() {
  return (
    <Suspense fallback={<Spinner label="Loading…" />}>
      <VouchersContent />
    </Suspense>
  );
}
