'use client';

// ---------------------------------------------------------------------------
// /ca-review/[assignmentId] — the CA review panel for a single assignment.
//   • Left: the taxpayer return summary + computed refund/payable.
//   • Right: the decision panel (comment box + Approve / Request-changes) and
//     the chronological review comment history.
// Data: GET /ca/assignments/{id} → AssignmentDetailDto.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import {
  Spinner,
  Alert,
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  Button,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import {
  getAssignment,
  caKeys,
  CaReturnSummaryCard,
  ReviewActionPanel,
  ReviewCommentThread,
} from '@/features/ca';

export default function CaReviewDetailPage({
  params,
}: {
  params: { assignmentId: string };
}) {
  const { assignmentId } = params;
  const t = useTranslations('caReview');

  const query = useQuery({
    queryKey: caKeys.assignment(assignmentId),
    queryFn: () => getAssignment(assignmentId),
  });

  return (
    <div className="space-y-6">
      <Link
        href="/ca-review"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-ink-500 hover:text-ink-800"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        {t('backToQueue')}
      </Link>

      {query.isLoading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : query.isError ? (
        <Alert variant="error" title={t('detailLoadError')}>
          {(query.error as ApiError).message}
        </Alert>
      ) : query.data ? (
        <>
          <div>
            <h1 className="text-2xl font-semibold text-ink-900">{t('reviewTitle')}</h1>
            <p className="text-sm text-ink-500">{t('reviewSubtitle')}</p>
          </div>

          <div className="grid gap-6 lg:grid-cols-2">
            {/* Return summary + computation */}
            <div className="space-y-6">
              <CaReturnSummaryCard summary={query.data.return} />
            </div>

            {/* Decision + history */}
            <div className="space-y-6">
              <ReviewActionPanel assignment={query.data} />

              <Card>
                <CardHeader>
                  <CardTitle>{t('historyTitle')}</CardTitle>
                </CardHeader>
                <CardContent>
                  <ReviewCommentThread comments={query.data.comments} />
                </CardContent>
              </Card>
            </div>
          </div>
        </>
      ) : null}
    </div>
  );
}
