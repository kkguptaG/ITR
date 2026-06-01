'use client';

// NoticeDetail — a single ITD notice: metadata header, status transition control,
// the recorded responses, and a form to add a response (with optional attachment).
// GET /notices/{id}; PATCH /notices/{id}:status; POST /notices/{id}/responses.

import { useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  FileText,
  Paperclip,
  Send,
  CheckCircle2,
  X,
  CalendarClock,
} from 'lucide-react';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Badge,
  Button,
  Select,
  Textarea,
  Spinner,
  Alert,
  EmptyState,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDate, formatDateTime, formatInr, formatBytes } from '@/lib/format';
import {
  getNotice,
  updateNoticeStatus,
  addNoticeResponse,
  supportKeys,
} from '../api';
import type { NoticeStatus } from '../types';
import {
  NOTICE_STATUSES,
  noticeStatusTone,
  fileToBase64,
  MAX_NOTICE_FILE_BYTES,
} from '../helpers';

function MetaRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <dt className="text-sm text-ink-500">{label}</dt>
      <dd className="text-right text-sm font-medium text-ink-900">{children}</dd>
    </div>
  );
}

export function NoticeDetail({ noticeId }: { noticeId: string }) {
  const t = useTranslations('support');
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [responseText, setResponseText] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: supportKeys.notice(noticeId),
    queryFn: () => getNotice(noticeId),
  });

  const changeStatus = useMutation({
    mutationFn: (status: NoticeStatus) => updateNoticeStatus(noticeId, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: supportKeys.notice(noticeId) });
      queryClient.invalidateQueries({ queryKey: supportKeys.notices });
    },
  });

  const addResponse = useMutation({
    mutationFn: async () => {
      const fileBase64 = file ? await fileToBase64(file) : null;
      return addNoticeResponse(noticeId, {
        responseText: responseText.trim(),
        fileName: file?.name ?? null,
        contentType: file?.type || null,
        fileBase64,
      });
    },
    onSuccess: () => {
      setResponseText('');
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
      queryClient.invalidateQueries({ queryKey: supportKeys.notice(noticeId) });
      queryClient.invalidateQueries({ queryKey: supportKeys.notices });
    },
  });

  function onPickFile(e: React.ChangeEvent<HTMLInputElement>) {
    const picked = e.target.files?.[0] ?? null;
    setFileError(null);
    if (picked && picked.size > MAX_NOTICE_FILE_BYTES) {
      setFileError(t('fileTooLarge', { max: formatBytes(MAX_NOTICE_FILE_BYTES) }));
      e.target.value = '';
      return;
    }
    setFile(picked);
  }

  if (query.isLoading) {
    return (
      <div className="flex justify-center py-16">
        <Spinner />
      </div>
    );
  }
  if (query.isError) {
    return (
      <Alert variant="error" title={t('noticeLoadError')}>
        {(query.error as ApiError).message}
      </Alert>
    );
  }
  if (!query.data) return null;

  const notice = query.data;

  function submitResponse(e: React.FormEvent) {
    e.preventDefault();
    if (responseText.trim().length === 0 || addResponse.isPending) return;
    addResponse.mutate();
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <Card>
        <CardContent className="flex flex-col gap-3 p-5 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-brand-50 text-brand-600">
              <FileText className="h-5 w-5" aria-hidden="true" />
            </span>
            <div>
              <h1 className="text-xl font-semibold text-ink-900">{notice.noticeType}</h1>
              <div className="mt-1.5 flex flex-wrap items-center gap-2">
                <Badge tone={noticeStatusTone[notice.status] ?? 'neutral'}>{notice.status}</Badge>
                {notice.section && <Badge tone="neutral">{notice.section}</Badge>}
                {notice.hasAttachment && (
                  <span className="inline-flex items-center gap-1 text-xs text-ink-500">
                    <Paperclip className="h-3.5 w-3.5" aria-hidden="true" />
                    {t('hasAttachment')}
                  </span>
                )}
              </div>
            </div>
          </div>
          <div className="w-full sm:w-44">
            <Select
              aria-label={t('changeStatus')}
              value={notice.status}
              disabled={changeStatus.isPending}
              onChange={(e) => changeStatus.mutate(e.target.value as NoticeStatus)}
              options={NOTICE_STATUSES.map((s) => ({
                value: s,
                label: t(`noticeStatus.${s}` as 'noticeStatus.Open'),
              }))}
            />
          </div>
        </CardContent>
      </Card>

      {changeStatus.isError && (
        <Alert variant="error" title={t('statusUpdateFailed')}>
          {(changeStatus.error as ApiError).message}
        </Alert>
      )}

      <div className="grid gap-5 lg:grid-cols-2">
        {/* Details */}
        <Card>
          <CardHeader>
            <CardTitle>{t('noticeDetails')}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="divide-y divide-ink-100">
              <MetaRow label={t('receivedAt')}>{formatDate(notice.receivedAt)}</MetaRow>
              <MetaRow label={t('dueDate')}>
                {notice.dueDate ? (
                  <span className="inline-flex items-center gap-1">
                    <CalendarClock className="h-3.5 w-3.5 text-ink-400" aria-hidden="true" />
                    {formatDate(notice.dueDate)}
                  </span>
                ) : (
                  '—'
                )}
              </MetaRow>
              {notice.din && <MetaRow label={t('din')}>{notice.din}</MetaRow>}
              {notice.demandAmount && (
                <MetaRow label={t('demandAmount')}>
                  <span className="text-payable-700">{formatInr(notice.demandAmount)}</span>
                </MetaRow>
              )}
              {notice.refundAmount && (
                <MetaRow label={t('refundAmount')}>
                  <span className="text-money-700">{formatInr(notice.refundAmount)}</span>
                </MetaRow>
              )}
            </dl>
            {notice.summary && (
              <div className="mt-4 rounded-xl bg-ink-50 p-3.5">
                <p className="text-xs font-medium uppercase tracking-wide text-ink-500">
                  {t('summaryLabel')}
                </p>
                <p className="mt-1 whitespace-pre-wrap text-sm text-ink-700">{notice.summary}</p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Responses */}
        <Card>
          <CardHeader>
            <CardTitle>{t('responses')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {notice.responses.length === 0 ? (
              <EmptyState
                icon={CheckCircle2}
                title={t('noResponsesTitle')}
                description={t('noResponsesBody')}
              />
            ) : (
              <ol className="space-y-3">
                {notice.responses.map((r) => (
                  <li key={r.id} className="rounded-xl border border-ink-200 p-3.5">
                    <div className="flex items-center justify-between gap-2">
                      <Badge tone="brand">{r.responseType || t('responseDefault')}</Badge>
                      <time className="text-xs text-ink-400">{formatDateTime(r.createdAt)}</time>
                    </div>
                    <p className="mt-2 whitespace-pre-wrap text-sm text-ink-700">{r.responseText}</p>
                    <div className="mt-2 flex flex-wrap items-center gap-3 text-xs text-ink-500">
                      {r.hasAttachment && (
                        <span className="inline-flex items-center gap-1">
                          <Paperclip className="h-3.5 w-3.5" aria-hidden="true" />
                          {t('hasAttachment')}
                        </span>
                      )}
                      {r.acknowledgementNo && (
                        <span>{t('ackNo', { no: r.acknowledgementNo })}</span>
                      )}
                    </div>
                  </li>
                ))}
              </ol>
            )}

            {/* Add response */}
            {addResponse.isError && (
              <Alert variant="error">{(addResponse.error as ApiError).message}</Alert>
            )}
            <form onSubmit={submitResponse} className="space-y-3 border-t border-ink-100 pt-4">
              <Textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                placeholder={t('responsePlaceholder')}
                rows={3}
                aria-label={t('addResponse')}
                disabled={addResponse.isPending}
              />
              {file && (
                <div className="flex items-center justify-between rounded-xl border border-ink-200 bg-ink-50 px-3.5 py-2.5">
                  <span className="flex min-w-0 items-center gap-2 text-sm text-ink-700">
                    <Paperclip className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
                    <span className="truncate">{file.name}</span>
                  </span>
                  <button
                    type="button"
                    onClick={() => {
                      setFile(null);
                      if (fileInputRef.current) fileInputRef.current.value = '';
                    }}
                    className="rounded-lg p-1 text-ink-400 hover:bg-ink-200 hover:text-ink-700"
                    aria-label={t('removeFile')}
                  >
                    <X className="h-4 w-4" aria-hidden="true" />
                  </button>
                </div>
              )}
              {fileError && <p className="text-xs font-medium text-red-600">{fileError}</p>}
              <div className="flex items-center justify-between">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => fileInputRef.current?.click()}
                >
                  <Paperclip className="h-4 w-4" aria-hidden="true" />
                  {t('attach')}
                </Button>
                <Button
                  type="submit"
                  loading={addResponse.isPending}
                  disabled={responseText.trim().length === 0}
                >
                  <Send className="h-4 w-4" aria-hidden="true" />
                  {t('recordResponse')}
                </Button>
              </div>
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf,.jpg,.jpeg,.png"
                className="hidden"
                onChange={onPickFile}
              />
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
