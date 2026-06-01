'use client';

// ---------------------------------------------------------------------------
// Step 4 — Deductions (Chapter VI-A). A list of section/amount deductions
// persisted to /returns/{id}/deductions, plus the 80C/80D gap-analysis advisor
// from POST /tax/recommendations (shown once at least some income exists).
// ---------------------------------------------------------------------------

import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { Button, CurrencyInput, Field, Select, Spinner } from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addDeduction,
  deleteDeduction,
  filingKeys,
  getRecommendations,
  listDeductions,
  updateDeduction,
} from '../api';
import type { DeductionDto } from '../types';
import { deductionSchema, DEDUCTION_SECTIONS, type DeductionFormValues } from '../schemas';
import { useWizard } from '../WizardContext';
import { useInvalidateReturn } from '../useReturn';
import { useHeadCrud } from '../useHeadCrud';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { EditableList } from '../components/EditableList';
import { DeductionRecommendations } from '../components/DeductionRecommendations';

export function DeductionsStep() {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const { returnId, goNext } = useWizard();
  const invalidate = useInvalidateReturn(returnId);

  const deductions = useHeadCrud<DeductionDto, Parameters<typeof addDeduction>[1]>(
    returnId,
    filingKeys.deductions(returnId),
    { list: listDeductions, add: addDeduction, update: updateDeduction, remove: deleteDeduction },
    invalidate,
  );

  // Recommendations advisor (best-effort; tolerates an early/empty return).
  const recsQuery = useQuery({
    queryKey: filingKeys.recommendations(returnId),
    queryFn: () => getRecommendations({ returnId }),
    retry: false,
    staleTime: 15_000,
  });

  return (
    <>
      <WizardStep title={t('deductionsTitle')} description={t('deductionsSubtitle')}>
        <EditableList<DeductionDto>
          items={deductions.query.data ?? []}
          getKey={(d) => d.id}
          addLabel={t('addDeduction')}
          emptyLabel={t('noDeductions')}
          deleting={deductions.deleteMutation.isPending}
          onDelete={(d) => deductions.deleteMutation.mutate(d.id)}
          renderSummary={(d) => (
            <div>
              <div className="font-medium text-ink-800">
                {d.section}
                {d.description ? ` · ${d.description}` : ''}
              </div>
              <div className="text-sm text-ink-500">
                {formatInr(d.amount)}
                {typeof d.eligibleAmount === 'number' && d.eligibleAmount !== d.amount
                  ? ` (${t('eligible')}: ${formatInr(d.eligibleAmount)})`
                  : ''}
              </div>
            </div>
          )}
          renderForm={(item, done) => (
            <DeductionForm
              defaultValues={
                item ? { section: item.section as DeductionFormValues['section'], description: item.description ?? '', amount: item.amount } : undefined
              }
              loading={deductions.addMutation.isPending || deductions.updateMutation.isPending}
              onCancel={done}
              onSubmit={(v) => {
                // The free-text note doubles as the engine sub-type ("severe" for 80U/80DD; an 80G
                // category like "100 no limit"). Blank ⇒ the section's default treatment.
                const body = { section: v.section, subType: v.description || null, description: v.description || null, amount: v.amount };
                const op = item
                  ? deductions.updateMutation.mutateAsync({ id: item.id, body })
                  : deductions.addMutation.mutateAsync(body);
                void op.then(done);
              }}
            />
          )}
        />

        {recsQuery.isLoading ? (
          <div className="flex justify-center py-4">
            <Spinner label={t('loadingRecommendations')} />
          </div>
        ) : recsQuery.data ? (
          <DeductionRecommendations data={recsQuery.data} />
        ) : null}
      </WizardStep>

      <WizardFooter
        primary={
          <Button type="button" onClick={goNext}>
            {tc('continue')}
          </Button>
        }
      />
    </>
  );
}

function DeductionForm({
  defaultValues,
  onSubmit,
  onCancel,
  loading,
}: {
  defaultValues?: Partial<DeductionFormValues>;
  onSubmit: (v: DeductionFormValues) => void;
  onCancel: () => void;
  loading?: boolean;
}) {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const { control, register, handleSubmit, formState: { errors } } = useForm<DeductionFormValues>({
    resolver: zodResolver(deductionSchema),
    defaultValues: { section: '80C', description: '', amount: 0, ...defaultValues },
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('section')} error={errors.section?.message} required>
          <Select {...register('section')}>
            {DEDUCTION_SECTIONS.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </Select>
        </Field>
        <Field label={t('amount')} error={errors.amount?.message}>
          <Controller
            control={control}
            name="amount"
            render={({ field }) => (
              <CurrencyInput
                value={field.value ?? null}
                onValueChange={(v) => field.onChange(v ?? 0)}
                onBlur={field.onBlur}
              />
            )}
          />
        </Field>
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          {tc('cancel')}
        </Button>
        <Button type="submit" loading={loading}>
          {tc('save')}
        </Button>
      </div>
    </form>
  );
}
