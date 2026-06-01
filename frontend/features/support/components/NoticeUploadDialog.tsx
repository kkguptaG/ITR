'use client';

// NoticeUploadDialog — record an ITD notice in the passive vault (Decision Log
// D-6). The user enters the notice metadata and optionally attaches a scanned
// copy, which is sent inline as base64 (CreateNoticeRequest.fileBase64).
// POST /notices → routes to the notice detail on success.

import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Paperclip, X } from 'lucide-react';
import { Modal, Button, Field, Select, Input, Textarea, Alert } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatBytes } from '@/lib/format';
import { createNotice, supportKeys } from '../api';
import {
  NOTICE_TYPES,
  toOptions,
  fileToBase64,
  MAX_NOTICE_FILE_BYTES,
} from '../helpers';

const schema = z.object({
  noticeType: z.string().min(1),
  section: z.string().trim().max(40).optional().or(z.literal('')),
  din: z.string().trim().max(40).optional().or(z.literal('')),
  receivedAt: z.string().min(1),
  dueDate: z.string().optional().or(z.literal('')),
  summary: z.string().trim().max(2000).optional().or(z.literal('')),
  demandAmount: z.string().optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export interface NoticeUploadDialogProps {
  open: boolean;
  onClose: () => void;
}

export function NoticeUploadDialog({ open, onClose }: NoticeUploadDialogProps) {
  const t = useTranslations('support');
  const tc = useTranslations('common');
  const router = useRouter();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      noticeType: NOTICE_TYPES[0],
      section: '',
      din: '',
      receivedAt: todayIso(),
      dueDate: '',
      summary: '',
      demandAmount: '',
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const fileBase64 = file ? await fileToBase64(file) : null;
      return createNotice({
        noticeType: values.noticeType,
        section: values.section?.trim() || null,
        din: values.din?.trim() || null,
        receivedAt: values.receivedAt ? new Date(values.receivedAt).toISOString() : null,
        dueDate: values.dueDate || null,
        summary: values.summary?.trim() || null,
        demandAmount: values.demandAmount ? Number(values.demandAmount) : null,
        fileName: file?.name ?? null,
        contentType: file?.type || null,
        fileBase64,
      });
    },
    onSuccess: (notice) => {
      void queryClient.invalidateQueries({ queryKey: supportKeys.notices });
      onClose();
      router.push(`/support/notices/${notice.id}`);
    },
  });

  useEffect(() => {
    if (!open) {
      mutation.reset();
      setFile(null);
      setFileError(null);
      reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

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

  const onSubmit = handleSubmit((values) => mutation.mutate(values));

  const errorMessage =
    mutation.error instanceof ApiError
      ? (mutation.error.firstFieldError ?? mutation.error.message)
      : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('newNoticeTitle')}
      description={t('newNoticeSubtitle')}
      size="lg"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            {tc('cancel')}
          </Button>
          <Button onClick={onSubmit} loading={mutation.isPending}>
            {t('saveNotice')}
          </Button>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {errorMessage && <Alert variant="error">{errorMessage}</Alert>}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('noticeType')} htmlFor="notice-type" required>
            <Select id="notice-type" options={toOptions(NOTICE_TYPES)} {...register('noticeType')} />
          </Field>
          <Field label={t('section')} htmlFor="notice-section" hint={t('sectionHint')}>
            <Input id="notice-section" placeholder="143(1)" {...register('section')} />
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('din')} htmlFor="notice-din" hint={t('dinHint')}>
            <Input id="notice-din" placeholder="CPC/2526/..." {...register('din')} />
          </Field>
          <Field label={t('demandAmount')} htmlFor="notice-demand" hint={t('demandHint')}>
            <Input
              id="notice-demand"
              type="number"
              inputMode="decimal"
              min={0}
              step="0.01"
              placeholder="0.00"
              {...register('demandAmount')}
            />
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field
            label={t('receivedAt')}
            htmlFor="notice-received"
            required
            error={errors.receivedAt ? t('receivedAtError') : null}
          >
            <Input
              id="notice-received"
              type="date"
              max={todayIso()}
              invalid={!!errors.receivedAt}
              {...register('receivedAt')}
            />
          </Field>
          <Field label={t('dueDate')} htmlFor="notice-due">
            <Input id="notice-due" type="date" {...register('dueDate')} />
          </Field>
        </div>

        <Field label={t('summaryLabel')} htmlFor="notice-summary" hint={t('summaryHint')}>
          <Textarea id="notice-summary" rows={3} placeholder={t('summaryPlaceholder')} {...register('summary')} />
        </Field>

        {/* Optional scanned copy */}
        <div>
          <p className="mb-1.5 text-sm font-medium text-ink-700">{t('attachment')}</p>
          {file ? (
            <div className="flex items-center justify-between rounded-xl border border-ink-200 bg-ink-50 px-3.5 py-2.5">
              <span className="flex min-w-0 items-center gap-2 text-sm text-ink-700">
                <Paperclip className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
                <span className="truncate">{file.name}</span>
                <span className="shrink-0 text-ink-400">({formatBytes(file.size)})</span>
              </span>
              <button
                type="button"
                onClick={() => {
                  setFile(null);
                  if (fileInputRef.current) fileInputRef.current.value = '';
                }}
                className="rounded-lg p-1 text-ink-400 hover:bg-ink-200 hover:text-ink-700"
                aria-label={tc('remove')}
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </button>
            </div>
          ) : (
            <Button
              type="button"
              variant="outline"
              onClick={() => fileInputRef.current?.click()}
            >
              <Paperclip className="h-4 w-4" aria-hidden="true" />
              {t('attachFile')}
            </Button>
          )}
          <input
            ref={fileInputRef}
            type="file"
            accept=".pdf,.jpg,.jpeg,.png"
            className="hidden"
            onChange={onPickFile}
          />
          {fileError && <p className="mt-1.5 text-xs font-medium text-red-600">{fileError}</p>}
          {!fileError && <p className="mt-1.5 text-xs text-ink-500">{t('attachmentHint')}</p>}
        </div>
      </form>
    </Modal>
  );
}
