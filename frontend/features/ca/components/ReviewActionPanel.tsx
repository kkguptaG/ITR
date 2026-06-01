'use client';

// ReviewActionPanel — the CA's decision controls: a comment box plus Approve and
// Request-changes actions wired to the CA API. Request-changes requires a note
// (the taxpayer must know what to fix); the approve note is an optional sign-off.
// On success we invalidate the assignment + queue queries so the panel + queue
// reflect the new status, and surface a confirmation banner.

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, RotateCcw } from 'lucide-react';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
  Button,
  Textarea,
  Field,
  Alert,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { approveReturn, requestChanges, caKeys } from '../api';
import type { AssignmentDetailDto, AssignmentDto } from '../types';

type ActionKind = 'approve' | 'request-changes';

export function ReviewActionPanel({ assignment }: { assignment: AssignmentDetailDto }) {
  const t = useTranslations('caReview');
  const queryClient = useQueryClient();

  const [comments, setComments] = useState('');
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ variant: 'success'; text: string } | null>(null);

  const isCompleted = assignment.status === 'Completed';
  const returnId = assignment.return.returnId;

  const mutation = useMutation<AssignmentDto, ApiError, ActionKind>({
    mutationFn: (kind) => {
      const body = { comments: comments.trim() || null };
      return kind === 'approve'
        ? approveReturn(returnId, body)
        : requestChanges(returnId, body);
    },
    onSuccess: (_data, kind) => {
      setComments('');
      setFieldError(null);
      setBanner({
        variant: 'success',
        text: kind === 'approve' ? t('approveSuccess') : t('changesSuccess'),
      });
      queryClient.invalidateQueries({ queryKey: caKeys.assignment(assignment.assignmentId) });
      queryClient.invalidateQueries({ queryKey: caKeys.all });
    },
  });

  function run(kind: ActionKind) {
    // Request-changes must carry a note so the taxpayer knows what to fix.
    if (kind === 'request-changes' && comments.trim().length === 0) {
      setFieldError(t('commentsRequired'));
      return;
    }
    setFieldError(null);
    setBanner(null);
    mutation.mutate(kind);
  }

  if (isCompleted) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t('decisionTitle')}</CardTitle>
        </CardHeader>
        <CardContent>
          <Alert variant="info" title={t('alreadyCompletedTitle')}>
            {t('alreadyCompletedBody')}
          </Alert>
        </CardContent>
      </Card>
    );
  }

  const pendingKind = mutation.isPending ? mutation.variables : undefined;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('decisionTitle')}</CardTitle>
        <CardDescription>{t('decisionHint')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {banner && (
          <Alert variant={banner.variant} title={banner.text} />
        )}
        {mutation.isError && (
          <Alert variant="error" title={t('actionFailed')}>
            {mutation.error.firstFieldError ?? mutation.error.message}
          </Alert>
        )}

        <Field
          label={t('commentsLabel')}
          htmlFor="ca-review-comments"
          hint={t('commentsHint')}
          error={fieldError}
        >
          <Textarea
            id="ca-review-comments"
            value={comments}
            onChange={(e) => {
              setComments(e.target.value);
              if (fieldError) setFieldError(null);
            }}
            placeholder={t('commentsPlaceholder')}
            rows={5}
            invalid={!!fieldError}
            disabled={mutation.isPending}
          />
        </Field>

        <div className="flex flex-col gap-3 sm:flex-row">
          <Button
            variant="primary"
            fullWidth
            loading={pendingKind === 'approve'}
            disabled={mutation.isPending}
            onClick={() => run('approve')}
          >
            <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            {t('approve')}
          </Button>
          <Button
            variant="outline"
            fullWidth
            loading={pendingKind === 'request-changes'}
            disabled={mutation.isPending}
            onClick={() => run('request-changes')}
          >
            <RotateCcw className="h-4 w-4" aria-hidden="true" />
            {t('requestChanges')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
