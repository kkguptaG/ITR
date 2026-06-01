'use client';

// ---------------------------------------------------------------------------
// NewReturnDialog — the "Start a new return" dialog used by /dashboard + /returns.
//   1. Pick an assessment year (defaults to the active AY from /tax/slabs).
//   2. Either let us auto-pick the ITR form (recommended) or choose it manually.
//   3. POST /returns -> route into the wizard at /returns/{id}/file/personal.
// On success the returns list cache is invalidated so the new draft appears.
// ---------------------------------------------------------------------------

import { useEffect, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';
import { Modal, Button, Field, Select, Alert, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatAssessmentYear } from '@/lib/format';
import type { ItrType } from '@/lib/api-types';
import {
  createReturn,
  getActiveAssessmentYear,
  returnsKeys,
} from '../api';
import type { CreateReturnBody, ReturnDetailDto } from '../types';

/** "AY2025-26" -> "AY2024-25" (the immediately preceding year), for late/belated filing. */
function previousAyCode(code: string): string | null {
  const m = /^AY(\d{4})-(\d{2})$/.exec(code);
  if (!m) return null;
  const start = Number(m[1]) - 1;
  const end = (start + 1) % 100;
  return `AY${start}-${String(end).padStart(2, '0')}`;
}

const ITR_OPTIONS: { value: ItrType; label: string }[] = [
  { value: 'ITR1', label: 'ITR-1 (Sahaj) — salary / pension, one house property' },
  { value: 'ITR2', label: 'ITR-2 — capital gains, multiple properties' },
  { value: 'ITR3', label: 'ITR-3 — business / profession, F&O' },
  { value: 'ITR4', label: 'ITR-4 (Sugam) — presumptive 44AD / 44ADA' },
];

const schema = z.object({
  assessmentYear: z.string().min(1),
  // 'auto' means "let the selector decide later" → send no itrType.
  itrChoice: z.enum(['auto', 'ITR1', 'ITR2', 'ITR3', 'ITR4']),
});

type FormValues = z.infer<typeof schema>;

export interface NewReturnDialogProps {
  open: boolean;
  onClose: () => void;
}

export function NewReturnDialog({ open, onClose }: NewReturnDialogProps) {
  const t = useTranslations('returns');
  const tc = useTranslations('common');
  const router = useRouter();
  const queryClient = useQueryClient();

  // The active assessment year drives the default + the option list.
  const ayQuery = useQuery({
    queryKey: returnsKeys.activeAy,
    queryFn: getActiveAssessmentYear,
    staleTime: 60 * 60_000, // an AY code is effectively static within a session
    enabled: open,
  });

  const activeAy = ayQuery.data?.assessmentYear;

  const ayOptions = useMemo(() => {
    if (!activeAy) return [];
    const codes = [activeAy];
    const prev = previousAyCode(activeAy);
    if (prev) codes.push(prev);
    return codes.map((code) => ({ value: code, label: formatAssessmentYear(code) }));
  }, [activeAy]);

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { assessmentYear: '', itrChoice: 'auto' },
  });

  // Seed the AY default once it loads (and reset cleanly each time the dialog opens).
  useEffect(() => {
    if (open && activeAy) {
      setValue('assessmentYear', activeAy);
    }
  }, [open, activeAy, setValue]);

  const mutation = useMutation({
    mutationFn: (values: FormValues) => {
      const body: CreateReturnBody = { assessmentYear: values.assessmentYear };
      if (values.itrChoice !== 'auto') body.itrType = values.itrChoice;
      return createReturn(body);
    },
    onSuccess: (created: ReturnDetailDto) => {
      // New draft should appear in any cached list immediately.
      void queryClient.invalidateQueries({ queryKey: returnsKeys.lists() });
      onClose();
      router.push(`/returns/${created.id}/file/personal`);
    },
  });

  // When the dialog closes, clear any submit error + form state for next time.
  useEffect(() => {
    if (!open) {
      mutation.reset();
      reset({ assessmentYear: activeAy ?? '', itrChoice: 'auto' });
    }
    // Only react to the open/close toggle.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const onSubmit = handleSubmit((values) => mutation.mutate(values));

  const errorMessage =
    mutation.error instanceof ApiError
      ? (mutation.error.problem.detail ?? mutation.error.message)
      : mutation.error
        ? tc('retry')
        : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('dialogTitle')}
      description={t('dialogSubtitle')}
      size="md"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            {tc('cancel')}
          </Button>
          <Button
            onClick={onSubmit}
            loading={mutation.isPending}
            disabled={!activeAy}
          >
            {t('createCta')}
          </Button>
        </>
      }
    >
      {ayQuery.isLoading ? (
        <div className="flex justify-center py-6">
          <Spinner label={tc('loading')} />
        </div>
      ) : ayQuery.isError ? (
        <Alert variant="error">{t('ayLoadError')}</Alert>
      ) : (
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          {errorMessage && <Alert variant="error">{errorMessage}</Alert>}

          <Field label={t('assessmentYear')} htmlFor="assessmentYear" required>
            <Select
              id="assessmentYear"
              options={ayOptions}
              invalid={!!errors.assessmentYear}
              {...register('assessmentYear')}
            />
          </Field>

          <Field
            label={t('itrForm')}
            htmlFor="itrChoice"
            hint={t('itrFormHint')}
          >
            <Select id="itrChoice" invalid={!!errors.itrChoice} {...register('itrChoice')}>
              <option value="auto">{t('itrAuto')}</option>
              {ITR_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </Select>
          </Field>

          <div className="flex items-start gap-2 rounded-xl bg-brand-50 p-3 text-sm text-brand-800">
            <Sparkles className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>{t('autoHelp')}</span>
          </div>
        </form>
      )}
    </Modal>
  );
}
